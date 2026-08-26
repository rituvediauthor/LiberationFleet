using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatRoom;

public record UpdateChatRoomCommand(
    int RoomId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    ChatRoomType RoomType,
    string Purpose,
    string PlaintextName,
    string PlaintextOldName,
    string PlaintextOldPurpose) : IRequest<ChatOperationResponse>;

public class UpdateChatRoomCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IGiftRepository giftRepository,
    ContentTenureService contentTenureService,
    CrewChatsProposalService crewChatsProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateChatRoomCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(UpdateChatRoomCommand request, CancellationToken cancellationToken)
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
        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        if (room.FleetId.HasValue)
        {
            return await HandleFleetUpdateAsync(request, userId, room, cancellationToken);
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ChatOperationResponse { Success = false, Message = "Encrypted chat room name is required." };
        }

        if (!room.CrewId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var membership = await membershipRepository.GetMembershipAsync(userId, room.CrewId.Value, cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var crew = await crewRepository.GetByIdAsync(room.CrewId.Value, cancellationToken);
        if (crew is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
            var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureCrewMemberCanCreateAsync(
                crew,
                membership,
                giftRepository,
                contentTenureService,
                cancellationToken);
            if (!canPropose)
            {
                return new ChatOperationResponse
                {
                    Success = false,
                    Message = proposeError ?? "You are not allowed to create proposals yet."
                };
            }

            var proposalId = await crewChatsProposalService.CreateProposalAsync(
                crew.Id,
                userId,
                CrewChatProposalAction.Update,
                CrewChatChangeDescriber.UpdateTitle,
                CrewChatChangeDescriber.BuildUpdateDescription(
                    request.PlaintextOldName,
                    request.PlaintextOldPurpose,
                    request.PlaintextName,
                    request.Purpose),
                room.Id,
                request.Purpose,
                room.RoomType,
                request.Nonce.Trim(),
                request.Ciphertext.Trim(),
                request.KeyVersion,
                room.IsAdultContent,
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
        room.Purpose = request.Purpose.Trim();
        room.LastActivityAt = utcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomName,
            ResourceId = room.Id.ToString(),
            CrewId = room.CrewId.Value,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Chat room updated."
        };
    }

    private async Task<ChatOperationResponse> HandleFleetUpdateAsync(
        UpdateChatRoomCommand request,
        int userId,
        ChatRoom room,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null || fleet.Id != room.FleetId)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        if (fleet.RequireApprovalForEdits)
        {
            var (canPropose, proposeError) = await ProposalCreationAuthorization.EnsureFleetMemberCanCreateAsync(
                fleet,
                membership,
                membership.Crew,
                giftRepository,
                contentTenureService,
                cancellationToken);
            if (!canPropose)
            {
                return new ChatOperationResponse
                {
                    Success = false,
                    Message = proposeError ?? "You are not allowed to create fleet proposals yet."
                };
            }

            var proposalId = await crewChatsProposalService.CreateFleetProposalAsync(
                fleet.Id,
                userId,
                CrewChatProposalAction.Update,
                CrewChatChangeDescriber.UpdateTitle,
                CrewChatChangeDescriber.BuildUpdateDescription(
                    request.PlaintextOldName,
                    request.PlaintextOldPurpose,
                    request.PlaintextName,
                    request.Purpose),
                room.Id,
                request.Purpose,
                room.RoomType,
                request.PlaintextName,
                room.IsAdultContent,
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

        room.Purpose = request.Purpose.Trim();
        room.Name = request.PlaintextName.Trim();
        room.LastActivityAt = DateTime.UtcNow;

        if (!string.IsNullOrWhiteSpace(request.Nonce) && !string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            var utcNow = DateTime.UtcNow;
            room.Name = string.Empty;
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
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Fleet chat room updated."
        };
    }
}
