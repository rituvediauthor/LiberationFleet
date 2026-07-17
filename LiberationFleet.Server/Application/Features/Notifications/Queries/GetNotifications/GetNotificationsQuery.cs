using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Queries.GetNotifications;

public record GetNotificationsQuery(string? Category, int Limit = 50, int? BeforeId = null) : IRequest<NotificationListResponse>;

public class GetNotificationsQueryHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUserRepository userRepository) : IRequestHandler<GetNotificationsQuery, NotificationListResponse>
{
    public async Task<NotificationListResponse> Handle(GetNotificationsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationListResponse { Success = false, Message = "Unauthorized." };
        }

        var category = ParseCategory(request.Category);
        var userId = currentUser.UserId.Value;
        var notifications = await notificationRepository.GetForUserAsync(
            userId,
            category,
            request.Limit,
            request.BeforeId,
            cancellationToken);
        var unreadCount = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);

        var actorIds = notifications
            .Where(n => n.ActorUserId.HasValue)
            .Select(n => n.ActorUserId!.Value)
            .Distinct()
            .ToList();
        var avatars = await userRepository.GetAvatarResourceIdsAsync(actorIds, cancellationToken);

        var items = notifications.Select(n =>
        {
            var dto = NotificationMapper.Map(n);
            if (n.ActorUserId.HasValue && avatars.TryGetValue(n.ActorUserId.Value, out var avatar))
            {
                dto.ActorAvatarResourceId = avatar;
            }

            return dto;
        }).ToList();

        return new NotificationListResponse
        {
            Success = true,
            Message = "Notifications loaded.",
            UnreadCount = unreadCount,
            Items = items
        };
    }

    private static NotificationFilterCategory? ParseCategory(string? category)
    {
        if (string.IsNullOrWhiteSpace(category) || category.Equals("all", StringComparison.OrdinalIgnoreCase))
        {
            return NotificationFilterCategory.All;
        }

        return Enum.TryParse<NotificationFilterCategory>(category, true, out var parsed)
            ? parsed
            : NotificationFilterCategory.All;
    }
}
