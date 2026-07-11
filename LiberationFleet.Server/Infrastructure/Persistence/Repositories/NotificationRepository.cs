using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class NotificationRepository(ApplicationDbContext context) : INotificationRepository
{
    public async Task<IReadOnlyList<Notification>> GetForUserAsync(
        int userId,
        NotificationFilterCategory? category,
        int limit,
        int? beforeId,
        CancellationToken cancellationToken = default)
    {
        var query = context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId);

        if (beforeId.HasValue)
        {
            query = query.Where(n => n.Id < beforeId.Value);
        }

        if (category is NotificationFilterCategory categoryValue && categoryValue != NotificationFilterCategory.All)
        {
            var kinds = NotificationCategoryMapper.GetKindsForCategory(categoryValue);
            query = query.Where(n => kinds.Contains(n.Kind));
        }

        return await query
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .Take(Math.Clamp(limit, 1, 100))
            .ToListAsync(cancellationToken);
    }

    public Task<int> GetUnreadCountAsync(int userId, CancellationToken cancellationToken = default) =>
        context.Notifications.CountAsync(n => n.UserId == userId && !n.IsRead, cancellationToken);

    public async Task<IReadOnlyList<Notification>> GetUnreadForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.Notifications
            .AsNoTracking()
            .Where(n => n.UserId == userId && !n.IsRead)
            .OrderByDescending(n => n.CreatedAt)
            .ThenByDescending(n => n.Id)
            .ToListAsync(cancellationToken);

    public async Task<int> MarkReadByContentAsync(
        int userId,
        string? actionUrlPrefix,
        int? relatedEntityId,
        CancellationToken cancellationToken = default)
    {
        var hasPrefix = !string.IsNullOrWhiteSpace(actionUrlPrefix);
        if (!hasPrefix && !relatedEntityId.HasValue)
        {
            return 0;
        }

        var query = context.Notifications.Where(n => n.UserId == userId && !n.IsRead);
        if (hasPrefix && relatedEntityId.HasValue)
        {
            var prefix = actionUrlPrefix!.Trim();
            var entityId = relatedEntityId.Value;
            query = query.Where(n => n.ActionUrl.StartsWith(prefix) || n.RelatedEntityId == entityId);
        }
        else if (hasPrefix)
        {
            var prefix = actionUrlPrefix!.Trim();
            query = query.Where(n => n.ActionUrl.StartsWith(prefix));
        }
        else
        {
            query = query.Where(n => n.RelatedEntityId == relatedEntityId!.Value);
        }

        return await query.ExecuteUpdateAsync(
            setters => setters.SetProperty(n => n.IsRead, true),
            cancellationToken);
    }

    public async Task AddAsync(Notification notification, CancellationToken cancellationToken = default) =>
        await context.Notifications.AddAsync(notification, cancellationToken);

    public async Task AddRangeAsync(IEnumerable<Notification> notifications, CancellationToken cancellationToken = default) =>
        await context.Notifications.AddRangeAsync(notifications, cancellationToken);

    public Task<Notification?> GetByIdForUserAsync(int notificationId, int userId, CancellationToken cancellationToken = default) =>
        context.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);

    public async Task MarkReadAsync(int notificationId, int userId, CancellationToken cancellationToken = default)
    {
        var notification = await context.Notifications
            .FirstOrDefaultAsync(n => n.Id == notificationId && n.UserId == userId, cancellationToken);
        if (notification is not null)
        {
            notification.IsRead = true;
        }
    }

    public async Task MarkAllReadAsync(int userId, CancellationToken cancellationToken = default)
    {
        await context.Notifications
            .Where(n => n.UserId == userId && !n.IsRead)
            .ExecuteUpdateAsync(setters => setters.SetProperty(n => n.IsRead, true), cancellationToken);
    }

    public async Task<IReadOnlyList<UserNotificationPreference>> GetPreferencesAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

    public async Task UpsertPreferencesAsync(int userId, IReadOnlyList<UserNotificationPreference> preferences, CancellationToken cancellationToken = default)
    {
        var existing = await context.UserNotificationPreferences
            .Where(p => p.UserId == userId)
            .ToListAsync(cancellationToken);

        foreach (var preference in preferences)
        {
            var match = existing.FirstOrDefault(p => p.Kind == preference.Kind);
            if (match is null)
            {
                preference.UserId = userId;
                await context.UserNotificationPreferences.AddAsync(preference, cancellationToken);
            }
            else
            {
                match.IsEnabled = preference.IsEnabled;
            }
        }
    }

    public async Task<bool> IsKindEnabledAsync(int userId, NotificationKind kind, CancellationToken cancellationToken = default)
    {
        var preference = await context.UserNotificationPreferences
            .AsNoTracking()
            .FirstOrDefaultAsync(p => p.UserId == userId && p.Kind == kind, cancellationToken);

        return preference?.IsEnabled ?? true;
    }

    public Task<bool> IsContentMutedAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default) =>
        context.UserMutedContents.AnyAsync(
            m => m.UserId == userId && m.ContentType == contentType && m.ResourceId == resourceId,
            cancellationToken);

    public async Task<IReadOnlyList<UserMutedContent>> GetMutedContentsAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.UserMutedContents
            .AsNoTracking()
            .Where(m => m.UserId == userId)
            .OrderByDescending(m => m.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddMutedContentAsync(UserMutedContent mutedContent, CancellationToken cancellationToken = default) =>
        await context.UserMutedContents.AddAsync(mutedContent, cancellationToken);

    public async Task RemoveMutedContentAsync(
        int userId,
        MutedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.UserMutedContents
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.ContentType == contentType && m.ResourceId == resourceId,
                cancellationToken);

        if (existing is not null)
        {
            context.UserMutedContents.Remove(existing);
        }
    }

    public Task<bool> IsContentHiddenAsync(int userId, MutedContentType contentType, int resourceId, CancellationToken cancellationToken = default) =>
        context.UserHiddenContents.AnyAsync(
            h => h.UserId == userId && h.ContentType == contentType && h.ResourceId == resourceId,
            cancellationToken);

    public async Task<IReadOnlyList<UserHiddenContent>> GetHiddenContentsAsync(int userId, CancellationToken cancellationToken = default) =>
        await context.UserHiddenContents
            .AsNoTracking()
            .Where(h => h.UserId == userId)
            .OrderByDescending(h => h.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task AddHiddenContentAsync(UserHiddenContent hiddenContent, CancellationToken cancellationToken = default) =>
        await context.UserHiddenContents.AddAsync(hiddenContent, cancellationToken);

    public async Task RemoveHiddenContentAsync(
        int userId,
        MutedContentType contentType,
        int resourceId,
        CancellationToken cancellationToken = default)
    {
        var existing = await context.UserHiddenContents
            .FirstOrDefaultAsync(
                h => h.UserId == userId && h.ContentType == contentType && h.ResourceId == resourceId,
                cancellationToken);

        if (existing is not null)
        {
            context.UserHiddenContents.Remove(existing);
        }
    }

    public async Task<IReadOnlyList<int>> GetCrewMemberUserIdsAsync(int crewId, int? excludeUserId, CancellationToken cancellationToken = default)
    {
        var query = context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.CrewId == crewId && !m.IsBanned);

        if (excludeUserId.HasValue)
        {
            query = query.Where(m => m.UserId != excludeUserId.Value);
        }

        return await query.Select(m => m.UserId).ToListAsync(cancellationToken);
    }
}
