using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Chats;

public class CrewChatsProposalService(
    IProposalRepository proposalRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    NotificationService notificationService,
    IFleetRepository fleetRepository,
    IUnitOfWork unitOfWork)
{
    public async Task<int> CreateProposalAsync(
        int crewId,
        int authorUserId,
        CrewChatProposalAction action,
        string proposalTitle,
        string proposalDescription,
        int? roomId,
        string purpose,
        ChatRoomType roomType,
        string? nameNonce,
        string? nameCiphertext,
        int keyVersion,
        bool isAdultContent,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewChatChange,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewChatChangeAsync(new ProposalCrewChatChange
        {
            Proposal = proposal,
            Action = action,
            RoomId = roomId,
            Title = proposalTitle,
            Description = proposalDescription,
            Purpose = purpose.Trim(),
            RoomType = roomType,
            NameNonce = nameNonce,
            NameCiphertext = nameCiphertext,
            KeyVersion = keyVersion <= 0 ? 1 : keyVersion,
            IsAdultContent = isAdultContent
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ProposalVotingService.EnsureAuthorApproveVoteAsync(
            proposalRepository,
            proposal,
            utcNow,
            cancellationToken);
        var statusBefore = proposal.Status;
        await ProposalVotingService.RecalculateAfterAuthorVoteAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            utcNow,
            cancellationToken);
        if (statusBefore != ProposalStatus.Approved && proposal.Status == ProposalStatus.Approved)
        {
            await TryApplyApprovedProposalAsync(proposal, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            NotificationPreview.BodyOrFallback(proposalDescription, "A crew chat change was proposed."),
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return proposal.Id;
    }

    public async Task<int> CreateFleetProposalAsync(
        int fleetId,
        int authorUserId,
        CrewChatProposalAction action,
        string proposalTitle,
        string proposalDescription,
        int? roomId,
        string purpose,
        ChatRoomType roomType,
        string plaintextName,
        bool isAdultContent,
        CancellationToken cancellationToken)
    {
        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            FleetId = fleetId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.FleetChatChange,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddCrewChatChangeAsync(new ProposalCrewChatChange
        {
            Proposal = proposal,
            Action = action,
            RoomId = roomId,
            Title = proposalTitle,
            Description = proposalDescription,
            Purpose = purpose.Trim(),
            RoomType = roomType,
            PlaintextName = plaintextName.Trim(),
            KeyVersion = 1,
            IsAdultContent = isAdultContent
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ProposalVotingService.EnsureAuthorApproveVoteAsync(
            proposalRepository,
            proposal,
            utcNow,
            cancellationToken);
        var statusBefore = proposal.Status;
        await ProposalVotingService.RecalculateAfterAuthorVoteAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            utcNow,
            cancellationToken);
        if (statusBefore != ProposalStatus.Approved && proposal.Status == ProposalStatus.Approved)
        {
            await TryApplyApprovedProposalAsync(proposal, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleetId, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewAsync(
                fleetCrew.CrewId,
                NotificationKind.NewFleetProposal,
                "New fleet proposal",
                NotificationPreview.BodyOrFallback(proposalDescription, "A fleet chat change was proposed."),
                $"/app/fleet/proposals/{proposal.Id}",
                relatedEntityId: proposal.Id,
                excludeUserId: authorUserId,
                cancellationToken: cancellationToken);
        }

        return proposal.Id;
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind is not (ProposalKind.CrewChatChange or ProposalKind.FleetChatChange)
            || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetCrewChatChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied)
        {
            return;
        }

        var utcNow = DateTime.UtcNow;

        if (proposal.Kind == ProposalKind.FleetChatChange)
        {
            switch (change.Action)
            {
                case CrewChatProposalAction.Create:
                    await ApplyFleetCreateAsync(proposal, change, utcNow, cancellationToken);
                    break;
                case CrewChatProposalAction.Update:
                    await ApplyFleetUpdateAsync(change, utcNow, cancellationToken);
                    break;
                case CrewChatProposalAction.Delete:
                    await ApplyDeleteAsync(change, utcNow, cancellationToken);
                    break;
            }

            change.IsApplied = true;
            return;
        }

        switch (change.Action)
        {
            case CrewChatProposalAction.Create:
                await ApplyCreateAsync(proposal, change, proposal.AuthorUserId, utcNow, cancellationToken);
                break;
            case CrewChatProposalAction.Update:
                await ApplyUpdateAsync(change, utcNow, cancellationToken);
                break;
            case CrewChatProposalAction.Delete:
                await ApplyDeleteAsync(change, utcNow, cancellationToken);
                break;
        }

        change.IsApplied = true;
    }

    private async Task ApplyCreateAsync(
        Proposal proposal,
        ProposalCrewChatChange change,
        int authorUserId,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(change.NameNonce) || string.IsNullOrWhiteSpace(change.NameCiphertext))
        {
            return;
        }

        var room = new ChatRoom
        {
            CrewId = proposal.CrewId!.Value,
            Name = string.Empty,
            Purpose = change.Purpose,
            RoomType = change.RoomType,
            CreatedByUserId = authorUserId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = change.IsAdultContent
        };

        await chatRepository.AddRoomAsync(room, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomName,
            ResourceId = room.Id.ToString(),
            CrewId = proposal.CrewId!.Value,
            AuthorUserId = authorUserId,
            KeyVersion = change.KeyVersion,
            Nonce = change.NameNonce.Trim(),
            Ciphertext = change.NameCiphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        change.RoomId = room.Id;

        var savedRoom = await chatRepository.GetRoomByIdWithAuthorAsync(room.Id, cancellationToken);
        var nameEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ChatRoomName,
            room.Id.ToString(),
            cancellationToken);

        if (savedRoom is not null)
        {
            var dto = ChatMapper.MapListItem(savedRoom, nameEnvelope);
            await chatRealtimeNotifier.NotifyRoomCreatedAsync(proposal.CrewId!.Value, dto, cancellationToken);
        }
    }

    private async Task ApplyFleetCreateAsync(
        Proposal proposal,
        ProposalCrewChatChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!proposal.FleetId.HasValue || string.IsNullOrWhiteSpace(change.PlaintextName))
        {
            return;
        }

        var room = new ChatRoom
        {
            FleetId = proposal.FleetId.Value,
            Name = change.PlaintextName.Trim(),
            Purpose = change.Purpose,
            RoomType = change.RoomType,
            CreatedByUserId = proposal.AuthorUserId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = change.IsAdultContent
        };

        await chatRepository.AddRoomAsync(room, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        change.RoomId = room.Id;
    }

    private async Task ApplyUpdateAsync(
        ProposalCrewChatChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RoomId.HasValue
            || string.IsNullOrWhiteSpace(change.NameNonce)
            || string.IsNullOrWhiteSpace(change.NameCiphertext))
        {
            return;
        }

        var room = await chatRepository.GetRoomByIdAsync(change.RoomId.Value, cancellationToken);
        if (room is null)
        {
            return;
        }

        room.Purpose = change.Purpose;
        room.RoomType = change.RoomType;
        room.LastActivityAt = utcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomName,
            ResourceId = room.Id.ToString(),
            CrewId = room.CrewId!.Value,
            AuthorUserId = room.CreatedByUserId,
            KeyVersion = change.KeyVersion,
            Nonce = change.NameNonce.Trim(),
            Ciphertext = change.NameCiphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);
    }

    private async Task ApplyFleetUpdateAsync(
        ProposalCrewChatChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RoomId.HasValue || string.IsNullOrWhiteSpace(change.PlaintextName))
        {
            return;
        }

        var room = await chatRepository.GetRoomByIdAsync(change.RoomId.Value, cancellationToken);
        if (room is null)
        {
            return;
        }

        room.Name = change.PlaintextName.Trim();
        room.Purpose = change.Purpose;
        room.RoomType = change.RoomType;
        room.LastActivityAt = utcNow;
    }

    private async Task ApplyDeleteAsync(
        ProposalCrewChatChange change,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        if (!change.RoomId.HasValue)
        {
            return;
        }

        var room = await chatRepository.GetRoomByIdAsync(change.RoomId.Value, cancellationToken);
        if (room is null)
        {
            return;
        }

        room.IsDeleted = true;
        room.LastActivityAt = utcNow;
    }
}
