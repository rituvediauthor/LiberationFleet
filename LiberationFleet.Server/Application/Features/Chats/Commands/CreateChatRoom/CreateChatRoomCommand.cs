using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.CreateChatRoom;

public record CreateChatRoomCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    ChatRoomType RoomType,
    string Purpose,
    string PlaintextName,
    bool IsAdultContent,
    bool IsFleetScope = false) : IRequest<ChatOperationResponse>;

public class CreateChatRoomCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    CrewChatsProposalService crewChatsProposalService,
    IChatRealtimeNotifier chatRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateChatRoomCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(CreateChatRoomCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.PlaintextName))
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room name is required." };
        }

        if (string.IsNullOrWhiteSpace(request.Purpose))
        {
            return new ChatOperationResponse { Success = false, Message = "Purpose is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (request.IsFleetScope)
        {
            return await HandleFleetCreateAsync(request, userId, membership.CrewId, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ChatOperationResponse { Success = false, Message = "Encrypted chat room name is required." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var proposalId = await crewChatsProposalService.CreateProposalAsync(
                crew.Id,
                userId,
                CrewChatProposalAction.Create,
                CrewChatChangeDescriber.CreateTitle,
                CrewChatChangeDescriber.BuildCreateDescription(request.PlaintextName, request.Purpose),
                roomId: null,
                request.Purpose,
                request.RoomType,
                request.Nonce.Trim(),
                request.Ciphertext.Trim(),
                request.KeyVersion,
                request.IsAdultContent,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Proposal submitted for crew approval.",
                ProposalsSubmitted = true,
                ProposalId = proposalId
            };
        }

        var utcNow = DateTime.UtcNow;
        var room = new ChatRoom
        {
            CrewId = membership.CrewId,
            Name = string.Empty,
            Purpose = request.Purpose.Trim(),
            RoomType = request.RoomType,
            CreatedByUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = request.IsAdultContent
        };

        await chatRepository.AddRoomAsync(room, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomName,
            ResourceId = room.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedRoom = await chatRepository.GetRoomByIdWithAuthorAsync(room.Id, cancellationToken);
        var nameEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ChatRoomName,
            room.Id.ToString(),
            cancellationToken);

        if (savedRoom is not null)
        {
            var dto = ChatMapper.MapListItem(savedRoom, nameEnvelope);
            await chatRealtimeNotifier.NotifyRoomCreatedAsync(membership.CrewId, dto, cancellationToken);
        }

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Chat room created.",
            RoomId = room.Id
        };
    }

    private async Task<ChatOperationResponse> HandleFleetCreateAsync(
        CreateChatRoomCommand request,
        int userId,
        int crewId,
        CancellationToken cancellationToken)
    {
        if (request.RoomType != ChatRoomType.Text)
        {
            return new ChatOperationResponse { Success = false, Message = "Fleet chat rooms only support text chat." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(crewId, cancellationToken);
        if (fleet is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        if (fleet.RequireApprovalForEdits)
        {
            var proposalId = await crewChatsProposalService.CreateFleetProposalAsync(
                fleet.Id,
                userId,
                CrewChatProposalAction.Create,
                CrewChatChangeDescriber.CreateTitle,
                CrewChatChangeDescriber.BuildCreateDescription(request.PlaintextName, request.Purpose),
                roomId: null,
                request.Purpose,
                request.RoomType,
                request.PlaintextName,
                request.IsAdultContent,
                cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Proposal submitted for fleet approval.",
                ProposalsSubmitted = true,
                ProposalId = proposalId
            };
        }

        var utcNow = DateTime.UtcNow;
        var hasEncryptedName = !string.IsNullOrWhiteSpace(request.Nonce) && !string.IsNullOrWhiteSpace(request.Ciphertext);
        var room = new ChatRoom
        {
            FleetId = fleet.Id,
            Name = hasEncryptedName ? string.Empty : request.PlaintextName.Trim(),
            Purpose = request.Purpose.Trim(),
            RoomType = ChatRoomType.Text,
            CreatedByUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = request.IsAdultContent
        };

        await chatRepository.AddRoomAsync(room, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (hasEncryptedName)
        {
            await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
            {
                ContentType = EncryptedContentType.ChatRoomName,
                ResourceId = room.Id.ToString(),
                FleetId = fleet.Id,
                AuthorUserId = userId,
                KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
                Nonce = request.Nonce.Trim(),
                Ciphertext = request.Ciphertext.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }, cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Fleet chat room created.",
            RoomId = room.Id
        };
    }
}
