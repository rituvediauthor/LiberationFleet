using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.ToggleChatMessageLike;

public record ToggleChatMessageLikeCommand(int RoomId, int MessageId) : IRequest<ChatMessageLikeToggleResponse>;

public class ChatMessageLikeToggleResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Liked { get; set; }
    public int LikeCount { get; set; }
}

public class ToggleChatMessageLikeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleChatMessageLikeCommand, ChatMessageLikeToggleResponse>
{
    public async Task<ChatMessageLikeToggleResponse> Handle(
        ToggleChatMessageLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatMessageLikeToggleResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatMessageLikeToggleResponse { Success = false, Message = "You are not in a crew." };
        }

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ChatMessageLikeToggleResponse { Success = false, Message = "Chat room not found." };
        }

        var message = await chatRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.ChatRoomId != room.Id)
        {
            return new ChatMessageLikeToggleResponse { Success = false, Message = "Message not found." };
        }

        var existing = await chatRepository.GetMessageLikeAsync(userId, message.Id, cancellationToken);
        bool liked;
        var utcNow = DateTime.UtcNow;

        if (existing is null)
        {
            var like = new ChatMessageLike
            {
                UserId = userId,
                ChatRoomMessageId = message.Id,
                CreatedAt = utcNow
            };

            if (message.AuthorUserId != userId && !message.IsAnonymous)
            {
                var actionUrl = room.FleetId.HasValue
                    ? $"/app/fleet/chats/{room.Id}?highlightId={message.Id}"
                    : $"/app/crew/chats/{room.Id}?highlightId={message.Id}";
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = message.AuthorUserId,
                    CrewId = room.CrewId ?? membership.CrewId,
                    Kind = NotificationKind.ChatMessageLiked,
                    Title = "Message liked",
                    Body = "Someone liked your chat message.",
                    ActionUrl = actionUrl,
                    RelatedEntityId = message.Id,
                    ActorUserId = userId
                }, cancellationToken);
                like.AuthorNotified = true;
            }

            await chatRepository.AddMessageLikeAsync(like, cancellationToken);
            liked = true;
        }
        else if (existing.RemovedAt is null)
        {
            existing.RemovedAt = utcNow;
            liked = false;
        }
        else
        {
            existing.RemovedAt = null;
            liked = true;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var counts = await chatRepository.GetActiveLikeCountsForMessagesAsync([message.Id], cancellationToken);
        counts.TryGetValue(message.Id, out var likeCount);

        // Room-group broadcast; clients merge like fields without replacing body/ciphertext.
        var dto = new ChatMessageDto
        {
            Id = message.Id,
            LikeCount = likeCount,
            LikedByCurrentUser = false
        };
        await chatRealtimeNotifier.NotifyMessageUpdatedAsync(membership.CrewId, room.Id, dto, cancellationToken);

        return new ChatMessageLikeToggleResponse
        {
            Success = true,
            Message = liked ? "Message liked." : "Message unliked.",
            Liked = liked,
            LikeCount = likeCount
        };
    }
}
