using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class DevicePushTokenRepository(ApplicationDbContext db) : IDevicePushTokenRepository
{
    public async Task UpsertAsync(DevicePushToken token, CancellationToken cancellationToken = default)
    {
        var existingByToken = await db.DevicePushTokens
            .FirstOrDefaultAsync(t => t.Token == token.Token, cancellationToken);

        if (existingByToken is not null)
        {
            existingByToken.UserId = token.UserId;
            existingByToken.Platform = token.Platform;
            existingByToken.DeviceId = token.DeviceId;
            existingByToken.LastSeenAt = DateTime.UtcNow;
            existingByToken.IsDisabled = false;
            return;
        }

        if (!string.IsNullOrWhiteSpace(token.DeviceId))
        {
            var sameDevice = await db.DevicePushTokens
                .Where(t => t.UserId == token.UserId && t.DeviceId == token.DeviceId)
                .ToListAsync(cancellationToken);
            foreach (var row in sameDevice)
            {
                db.DevicePushTokens.Remove(row);
            }
        }

        token.CreatedAt = DateTime.UtcNow;
        token.LastSeenAt = DateTime.UtcNow;
        token.IsDisabled = false;
        await db.DevicePushTokens.AddAsync(token, cancellationToken);
    }

    public async Task RemoveByTokenAsync(int userId, string token, CancellationToken cancellationToken = default)
    {
        var rows = await db.DevicePushTokens
            .Where(t => t.UserId == userId && t.Token == token)
            .ToListAsync(cancellationToken);
        db.DevicePushTokens.RemoveRange(rows);
    }

    public async Task RemoveAllForUserAsync(int userId, CancellationToken cancellationToken = default)
    {
        var rows = await db.DevicePushTokens
            .Where(t => t.UserId == userId)
            .ToListAsync(cancellationToken);
        db.DevicePushTokens.RemoveRange(rows);
    }

    public async Task<IReadOnlyList<DevicePushToken>> GetActiveForUserAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        return await db.DevicePushTokens
            .AsNoTracking()
            .Where(t => t.UserId == userId && !t.IsDisabled)
            .ToListAsync(cancellationToken);
    }

    public async Task DisableAsync(int tokenId, CancellationToken cancellationToken = default)
    {
        var row = await db.DevicePushTokens.FirstOrDefaultAsync(t => t.Id == tokenId, cancellationToken);
        if (row is not null)
        {
            row.IsDisabled = true;
        }
    }
}
