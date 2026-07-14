using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposal;

public record CreateProposalCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds) : IRequest<ProposalOperationResponse>;

public class CreateProposalCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(CreateProposalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProposalOperationResponse { Success = false, Message = "Encrypted proposal content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Crew not found." };
        }

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var crewTenureDays = await contentTenureService.GetCrewTenureDaysAsync(
            userId,
            membership.CrewId,
            cancellationToken);

        if (!CrewContentPermissionService.CanCreateProposals(
                crew,
                membership,
                giftStats.LifetimeContributions,
                crewTenureDays))
        {
            return new ProposalOperationResponse
            {
                Success = false,
                Message = "You are not allowed to create proposals in this crew yet."
            };
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };
        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);

        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.Proposal,
            ResourceId = proposal.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            membership.CrewId,
            NotificationKind.NewProposal,
            "New proposal",
            "A new crew proposal was submitted.",
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: userId,
            cancellationToken: cancellationToken);

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.Proposal,
            ResourceId = proposal.Id,
            ActionUrl = $"/app/crew/proposals/{proposal.Id}",
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds)
        }, cancellationToken);

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Proposal created.",
            ProposalId = proposal.Id
        };
    }
}
