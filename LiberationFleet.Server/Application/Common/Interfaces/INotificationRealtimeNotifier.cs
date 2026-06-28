using LiberationFleet.Server.Application.Features.Notifications.Contracts;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface INotificationRealtimeNotifier
{
    Task NotifyReceivedAsync(int userId, NotificationDto notification, CancellationToken cancellationToken = default);

    Task NotifyUnreadCountUpdatedAsync(int userId, int unreadCount, CancellationToken cancellationToken = default);
}
