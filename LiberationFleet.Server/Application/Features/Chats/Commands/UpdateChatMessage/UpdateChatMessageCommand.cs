using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatMessage;

public record UpdateChatMessageCommand(
    int RoomId,
    int MessageId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<ChatOperationResponse>;

public class UpdateChatMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateChatMessageCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(UpdateChatMessageCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ChatOperationResponse { Success = false, Message = "Encrypted message content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || room.CrewId != membership.CrewId)
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        var message = await chatRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.ChatRoomId != room.Id || message.IsDeleted)
        {
            return new ChatOperationResponse { Success = false, Message = "Message not found." };
        }

        if (message.AuthorUserId != userId)
        {
            return new ChatOperationResponse { Success = false, Message = "Only the author can edit this message." };
        }

        var utcNow = DateTime.UtcNow;
        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomMessage,
            ResourceId = message.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var envelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ChatRoomMessage,
            message.Id.ToString(),
            cancellationToken);
        if (envelope is not null)
        {
            var messageDto = ChatMapper.MapMessage(message, envelope);
            await chatRealtimeNotifier.NotifyMessageUpdatedAsync(membership.CrewId, room.Id, messageDto, cancellationToken);
        }

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Message updated.",
            MessageId = message.Id
        };
    }
}
