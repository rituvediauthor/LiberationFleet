using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatMessage;

public record UpdateChatMessageCommand(
    int RoomId,
    int MessageId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    string? Body,
    IReadOnlyList<int> MentionedUserIds) : IRequest<ChatOperationResponse>;

public class UpdateChatMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateChatMessageCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(UpdateChatMessageCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        var isFleetRoom = !room.CrewId.HasValue && room.FleetId.HasValue;
        var hasEncryptedPayload = !string.IsNullOrWhiteSpace(request.Nonce) && !string.IsNullOrWhiteSpace(request.Ciphertext);
        if (isFleetRoom)
        {
            if (!hasEncryptedPayload && string.IsNullOrWhiteSpace(request.Body))
            {
                return new ChatOperationResponse { Success = false, Message = "Message content is required." };
            }
        }
        else if (!hasEncryptedPayload)
        {
            return new ChatOperationResponse { Success = false, Message = "Encrypted message content is required." };
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

        if (isFleetRoom && !hasEncryptedPayload)
        {
            message.Body = request.Body!.Trim();
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var fleetMessageDto = ChatMapper.MapMessage(message, envelope: null);
            await chatRealtimeNotifier.NotifyMessageUpdatedAsync(membership.CrewId, room.Id, fleetMessageDto, cancellationToken);

            await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
            {
                CrewId = membership.CrewId,
                FleetId = room.FleetId,
                AuthorUserId = userId,
                ContentType = MentionedContentType.ChatRoomMessage,
                ResourceId = message.Id,
                ParentResourceId = room.Id,
                ActionUrl = $"/app/fleet/chats/{room.Id}?messageId={message.Id}",
                MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
                IsUpdate = true
            }, cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Message updated.",
                MessageId = message.Id
            };
        }

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ChatRoomMessage,
            ResourceId = message.Id.ToString(),
            CrewId = isFleetRoom ? null : membership.CrewId,
            FleetId = isFleetRoom ? room.FleetId : null,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        if (isFleetRoom)
        {
            message.Body = null;
        }

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

        if (isFleetRoom)
        {
            await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
            {
                CrewId = membership.CrewId,
                FleetId = room.FleetId,
                AuthorUserId = userId,
                ContentType = MentionedContentType.ChatRoomMessage,
                ResourceId = message.Id,
                ParentResourceId = room.Id,
                ActionUrl = $"/app/fleet/chats/{room.Id}?messageId={message.Id}",
                MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
                IsUpdate = true
            }, cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Message updated.",
                MessageId = message.Id
            };
        }

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ChatRoomMessage,
            ResourceId = message.Id,
            ParentResourceId = room.Id,
            ActionUrl = $"/app/crew/chats/{room.Id}?messageId={message.Id}",
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            IsUpdate = true
        }, cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Message updated.",
            MessageId = message.Id
        };
    }
}
