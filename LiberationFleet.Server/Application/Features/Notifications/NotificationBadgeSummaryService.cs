using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;

namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Builds the filtered badge summary used by HTTP badges and SignalR pushes.
/// </summary>
public class NotificationBadgeSummaryService(
    INotificationRepository notificationRepository,
    IUserBlockRepository userBlockRepository)
{
    public async Task<NotificationBadgeSummaryResponse> GetForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var notifications = await notificationRepository.GetUnreadForUserAsync(userId, cancellationToken);
        var preferences = await notificationRepository.GetPreferencesAsync(userId, cancellationToken);
        var mutedContents = await notificationRepository.GetMutedContentsAsync(userId, cancellationToken);
        var hiddenContents = await notificationRepository.GetHiddenContentsAsync(userId, cancellationToken);
        var hiddenUserIds = await userBlockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);

        return NotificationBadgeBuilder.Build(
            notifications,
            preferences,
            mutedContents,
            hiddenContents,
            hiddenUserIds);
    }
}
