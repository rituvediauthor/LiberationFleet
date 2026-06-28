using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Hubs;
using Microsoft.AspNetCore.SignalR;

namespace LiberationFleet.Server.Infrastructure.Realtime;

public class NotificationRealtimeNotifier(IHubContext<NotificationHub> hubContext)
    : Application.Common.Interfaces.INotificationRealtimeNotifier
{
    public Task NotifyReceivedAsync(int userId, NotificationDto notification, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(NotificationHub.UserGroup(userId))
            .SendAsync("NotificationReceived", notification, cancellationToken);

    public Task NotifyUnreadCountUpdatedAsync(int userId, int unreadCount, CancellationToken cancellationToken = default) =>
        hubContext.Clients
            .Group(NotificationHub.UserGroup(userId))
            .SendAsync("UnreadCountUpdated", unreadCount, cancellationToken);
}
