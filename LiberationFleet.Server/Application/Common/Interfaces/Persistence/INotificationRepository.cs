using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface INotificationRepository
{
    Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId,
        NotificationFilterCategory? category,
        int limit,
        int? beforeId,
        IReadOnlySet<NotificationKind>? excludeKinds = null,
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Unread rows for badge building. Optionally excludes disabled kinds in SQL.
    /// Not ordered — badge aggregation does not need creation order.
    /// </summary>
    Task<IReadOnlyList<Notification>> GetUnreadForUserAsync(
        int userId,
        IReadOnlySet<NotificationKind>? excludeKinds = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Users in <paramref name="userIds"/> who have explicitly disabled <paramref name="kind"/>.
    /// </summary>
    Task<IReadOnlySet<int>> GetUserIdsWithKindDisabledAsync(
        IReadOnlyCollection<int> userIds,
        NotificationKind kind,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Users in <paramref name="userIds"/> who muted the given content.
    /// </summary>
    Task<IReadOnlySet<int>> GetUserIdsWithContentMutedAsync(
        IReadOnlyCollection<int> userIds,
        MutedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default);

    Task<int> MarkReadByContentAsync(
        int userId,
        string? actionUrlPrefix,
        int? relatedEntityId,
        CancellationToken cancellationToken = default);

    Task<int> MarkReadByKindAsync(
        int userId,
        NotificationKind kind,
        int? actorUserId = null,
        CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdForUserAsync(int notificationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>
    /// Tracked notifications for a user matching kind and related entity (for in-place updates).
    /// </summary>
    Task<IReadOnlyList<Notification>> GetForUserByKindAndRelatedAsync(
        int userId,
        NotificationKind kind,
        int relatedEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Marks a single notification read when it does not require an outstanding action.
    /// Returns false when the row is missing or still requires action.
    /// </summary>
    Task<bool> TryMarkReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default);

    /// <summary>Marks all non-action-required unread notifications for the user.</summary>
    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);

    /// <summary>Tracked unread rows that still have RequiresAction (for stale-action sweeps).</summary>
    Task<IReadOnlyList<Notification>> GetUnreadRequiresActionForUserAsync(
        int userId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears RequiresAction and marks matching notifications read (gift confirm, quest confirm, etc.).
    /// </summary>
    Task<int> ResolveRequiresActionAsync(
        int userId,
        NotificationKind kind,
        int relatedEntityId,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Clears RequiresAction and marks read for every user with matching kind + related entity
    /// (crew-wide workflows such as broken-item confirmation).
    /// </summary>
    Task<int> ResolveRequiresActionByRelatedAsync(
        NotificationKind kind,
        int relatedEntityId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotificationPreference>> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default);

    Task UpsertPreferencesAsync(int userId, IReadOnlyList<UserNotificationPreference> preferences, CancellationToken cancellationToken = default);

    Task<bool> IsKindEnabledAsync(int userId, NotificationKind kind, CancellationToken cancellationToken = default);

    Task<bool> IsContentMutedAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<UserMutedContent?> FindTrackedMuteAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMutedContent>> GetMutedContentsAsync(int userId, CancellationToken cancellationToken = default);

    Task AddMutedContentAsync(UserMutedContent mutedContent, CancellationToken cancellationToken = default);

    Task RemoveMutedContentAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<bool> IsContentHiddenAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserHiddenContent>> GetHiddenContentsAsync(int userId, CancellationToken cancellationToken = default);

    Task AddHiddenContentAsync(UserHiddenContent hiddenContent, CancellationToken cancellationToken = default);

    Task RemoveHiddenContentAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetCrewMemberUserIdsAsync(int crewId, int? excludeUserId, CancellationToken cancellationToken = default);
}
