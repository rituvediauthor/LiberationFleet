using System.Collections.Concurrent;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Services;

/// <summary>
/// Throttled presence writer. Reuses <see cref="Domain.Entities.User.LastLoginAt"/>
/// as last-active for existing crewmate/friend API contracts.
/// </summary>
public sealed class UserPresenceService(
    IServiceScopeFactory scopeFactory,
    ILogger<UserPresenceService> logger) : IUserPresenceService
{
    /// <summary>Minimum gap between DB writes for the same user (per process).</summary>
    internal static readonly TimeSpan WriteThrottle = TimeSpan.FromMinutes(2);

    /// <summary>DB guard so multi-instance hosts also skip unnecessary updates.</summary>
    internal static readonly TimeSpan DbStaleThreshold = TimeSpan.FromMinutes(2);

    private readonly ConcurrentDictionary<int, DateTime> _lastAttemptUtc = new();

    public void RecordActivity(int userId)
    {
        if (userId <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (_lastAttemptUtc.TryGetValue(userId, out var last) && now - last < WriteThrottle)
        {
            return;
        }

        _lastAttemptUtc[userId] = now;

        _ = PersistAsync(userId);
    }

    private async Task PersistAsync(int userId)
    {
        try
        {
            await using var scope = scopeFactory.CreateAsyncScope();
            var db = scope.ServiceProvider.GetRequiredService<ApplicationDbContext>();
            var cutoff = DateTime.UtcNow - DbStaleThreshold;
            var now = DateTime.UtcNow;

            var user = await db.Users
                .FirstOrDefaultAsync(u => u.Id == userId);
            if (user is null)
            {
                return;
            }

            if (user.LastLoginAt.HasValue && user.LastLoginAt.Value >= cutoff)
            {
                return;
            }

            user.LastLoginAt = now;
            await db.SaveChangesAsync();
        }
        catch (Exception ex)
        {
            logger.LogDebug(ex, "Failed to record presence for user {UserId}", userId);
        }
    }
}
