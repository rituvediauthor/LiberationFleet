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
        // Load filters first so unread rows can exclude disabled kinds in SQL.
        var preferences = await notificationRepository.GetPreferencesAsync(userId, cancellationToken);
        var disabledKinds = NotificationLegacySupport.ExpandDisabledKinds(
            preferences.Where(p => !p.IsEnabled).Select(p => p.Kind));

        var mutedContents = await notificationRepository.GetMutedContentsAsync(userId, cancellationToken);
        var hiddenContents = await notificationRepository.GetHiddenContentsAsync(userId, cancellationToken);
        var hiddenUserIds = await userBlockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var notifications = await notificationRepository.GetUnreadForUserAsync(
            userId,
            disabledKinds.Count > 0 ? disabledKinds : null,
            cancellationToken);

        return NotificationBadgeBuilder.Build(
            notifications,
            preferences,
            mutedContents,
            hiddenContents,
            hiddenUserIds);
    }
}
