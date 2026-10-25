using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.SendChatMessage;

public record SendChatMessageCommand(
    int RoomId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    string? Body,
    IReadOnlyList<int> MentionedUserIds,
    bool IsAnonymous,
    string? NotificationPreview = null) : IRequest<ChatOperationResponse>;

public class SendChatMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<SendChatMessageCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(SendChatMessageCommand request, CancellationToken cancellationToken)
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

        if (room.RoomType != ChatRoomType.Text)
        {
            return new ChatOperationResponse { Success = false, Message = "Text messages are not supported in this room type." };
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

        var utcNow = DateTime.UtcNow;
        var message = new ChatRoomMessage
        {
            ChatRoomId = room.Id,
            AuthorUserId = userId,
            CreatedAt = utcNow,
            IsAnonymous = request.IsAnonymous,
            Body = isFleetRoom && !hasEncryptedPayload ? request.Body!.Trim() : null
        };

        await chatRepository.AddMessageAsync(message, cancellationToken);
        room.LastActivityAt = utcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        EncryptedContentEnvelope? envelope = null;
        if (isFleetRoom && hasEncryptedPayload)
        {
            envelope = new EncryptedContentEnvelope
            {
                ContentType = EncryptedContentType.ChatRoomMessage,
                ResourceId = message.Id.ToString(),
                FleetId = room.FleetId,
                AuthorUserId = userId,
                KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
                Nonce = request.Nonce.Trim(),
                Ciphertext = request.Ciphertext.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            };

            await cryptoRepository.UpsertEnvelopeAsync(envelope, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        else if (!isFleetRoom)
        {
            envelope = new EncryptedContentEnvelope
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
            };

            await cryptoRepository.UpsertEnvelopeAsync(envelope, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var savedMessage = await chatRepository.GetMessageByIdWithAuthorAsync(message.Id, cancellationToken);
        if (savedMessage is not null)
        {
            var messageDto = ChatMapper.MapMessage(savedMessage, envelope, viewerUserId: null, allowKick: room.CrewId.HasValue);
            await chatRealtimeNotifier.NotifyMessageSentAsync(membership.CrewId, room.Id, messageDto, cancellationToken);

            var author = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
            var actorName = request.IsAnonymous
                ? "Anonymous"
                : UserDisplay.Name(author);
            int? notificationActorUserId = request.IsAnonymous ? null : userId;
            var chatTitle = NotificationCopy.PostedInChat(actorName, room.Name);
            var chatPreview = !string.IsNullOrWhiteSpace(request.NotificationPreview)
                ? request.NotificationPreview
                : request.Body;
            var chatBody = NotificationCopy.PreviewOr(chatPreview, "Open the chat to read the message.");

            if (isFleetRoom)
            {
                var fleetCrews = await fleetRepository.GetFleetCrewsAsync(room.FleetId!.Value, cancellationToken);
                foreach (var fleetCrew in fleetCrews)
                {
                    await chatRealtimeNotifier.NotifyRoomActivityUpdatedAsync(fleetCrew.CrewId, room.Id, utcNow, cancellationToken);
                    await notificationService.NotifyCrewIfNotMutedAsync(
                        fleetCrew.CrewId,
                        NotificationKind.NewFleetChatMessage,
                        MutedContentType.ChatRoom,
                        room.Id,
                        chatTitle,
                        chatBody,
                        $"/app/fleet/chats/{room.Id}?messageId={message.Id}",
                        relatedEntityId: message.Id,
                        secondaryEntityId: room.Id,
                        excludeUserId: userId,
                        actorUserId: notificationActorUserId,
                        omitActorIdentity: request.IsAnonymous,
                        cancellationToken: cancellationToken);
                }

                await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
                {
                    CrewId = membership.CrewId,
                    FleetId = room.FleetId,
                    AuthorUserId = userId,
                    OmitNotificationActorIdentity = request.IsAnonymous,
                    NotificationAuthorName = actorName,
                    ContentType = MentionedContentType.ChatRoomMessage,
                    ResourceId = message.Id,
                    ParentResourceId = room.Id,
                    ActionUrl = $"/app/fleet/chats/{room.Id}?messageId={message.Id}",
                    MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
                    Preview = chatPreview
                }, cancellationToken);
            }
            else
            {
                await chatRealtimeNotifier.NotifyRoomActivityUpdatedAsync(membership.CrewId, room.Id, utcNow, cancellationToken);

                await notificationService.NotifyCrewIfNotMutedAsync(
                    membership.CrewId,
                    NotificationKind.NewChatMessage,
                    MutedContentType.ChatRoom,
                    room.Id,
                    chatTitle,
                    chatBody,
                    $"/app/crew/chats/{room.Id}?messageId={message.Id}",
                    relatedEntityId: message.Id,
                    secondaryEntityId: room.Id,
                    excludeUserId: userId,
                    actorUserId: notificationActorUserId,
                    omitActorIdentity: request.IsAnonymous,
                    cancellationToken: cancellationToken);

                await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
                {
                    CrewId = membership.CrewId,
                    AuthorUserId = userId,
                    OmitNotificationActorIdentity = request.IsAnonymous,
                    NotificationAuthorName = actorName,
                    ContentType = MentionedContentType.ChatRoomMessage,
                    ResourceId = message.Id,
                    ParentResourceId = room.Id,
                    ActionUrl = $"/app/crew/chats/{room.Id}?messageId={message.Id}",
                    MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
                    Preview = chatPreview
                }, cancellationToken);
            }
        }

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Message sent.",
            MessageId = message.Id
        };
    }
}
