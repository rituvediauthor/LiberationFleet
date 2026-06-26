using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
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
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    CrewChatsProposalService crewChatsProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateChatRoomCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(UpdateChatRoomCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ChatOperationResponse { Success = false, Message = "Encrypted chat room name is required." };
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

        if (!await membershipRepository.IsUserInCrewAsync(userId, room.CrewId, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var crew = await crewRepository.GetByIdAsync(room.CrewId, cancellationToken);
        if (crew is null)
        {
            return new ChatOperationResponse { Success = false, Message = "Crew not found." };
        }

        if (crew.RequireApprovalForEdits)
        {
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
                request.RoomType,
                request.Nonce.Trim(),
                request.Ciphertext.Trim(),
                request.KeyVersion,
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
        room.RoomType = request.RoomType;
        room.LastActivityAt = utcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomName,
            ResourceId = room.Id.ToString(),
            CrewId = room.CrewId,
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
            Message = "Chat room updated.",
            RoomId = room.Id
        };
    }
}
