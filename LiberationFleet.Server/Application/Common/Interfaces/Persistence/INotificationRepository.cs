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
        CancellationToken cancellationToken = default);

    Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default);

    Task AddAsync(Notification notification, CancellationToken cancellationToken = default);

    Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default);

    Task<Notification?> GetByIdForUserAsync(int notificationId, int userId, CancellationToken cancellationToken = default);

    Task MarkReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default);

    Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserNotificationPreference>> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default);

    Task UpsertPreferencesAsync(int userId, IReadOnlyList<UserNotificationPreference> preferences, CancellationToken cancellationToken = default);

    Task<bool> IsKindEnabledAsync(int userId, NotificationKind kind, CancellationToken cancellationToken = default);

    Task<bool> IsContentMutedAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserMutedContent>> GetMutedContentsAsync(int userId, CancellationToken cancellationToken = default);

    Task AddMutedContentAsync(UserMutedContent mutedContent, CancellationToken cancellationToken = default);

    Task RemoveMutedContentAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<bool> IsContentHiddenAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<UserHiddenContent>> GetHiddenContentsAsync(int userId, CancellationToken cancellationToken = default);

    Task AddHiddenContentAsync(UserHiddenContent hiddenContent, CancellationToken cancellationToken = default);

    Task RemoveHiddenContentAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> GetCrewMemberUserIdsAsync(int crewId, int? excludeUserId, CancellationToken cancellationToken = default);
}
