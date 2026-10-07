using LiberationFleet.Server.Application.Features.Notifications.Contracts;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IPushNotificationSender
{
    /// <summary>
    /// Send OS push for a persisted in-app notification. No-ops when providers are not configured.
    /// Never throws to callers — failures are logged; invalid tokens are disabled.
    /// </summary>
    Task SendAsync(
        int userId,
        NotificationDto notification,
        int? badgeCount = null,
        CancellationToken cancellationToken = default);
}
