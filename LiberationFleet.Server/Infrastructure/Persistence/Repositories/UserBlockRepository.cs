using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class UserBlockRepository : IUserBlockRepository
{
    private readonly ApplicationDbContext _context;

    public UserBlockRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> IsBlockedAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default) =>
        _context.UserBlocks.AnyAsync(
            b => b.BlockerUserId == blockerUserId && b.BlockedUserId == blockedUserId,
            cancellationToken);

    public Task<UserBlock?> GetBlockAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default) =>
        _context.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockerUserId == blockerUserId && b.BlockedUserId == blockedUserId,
            cancellationToken);

    public async Task<IReadOnlySet<int>> GetBlockedUserIdsAsync(int blockerUserId, CancellationToken cancellationToken = default)
    {
        var ids = await _context.UserBlocks
            .Where(b => b.BlockerUserId == blockerUserId)
            .Select(b => b.BlockedUserId)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public async Task<IReadOnlySet<int>> GetHiddenUserIdsForViewerAsync(int viewerUserId, CancellationToken cancellationToken = default)
    {
        var blockedByViewer = await _context.UserBlocks
            .Where(b => b.BlockerUserId == viewerUserId)
            .Select(b => b.BlockedUserId)
            .ToListAsync(cancellationToken);

        var blockersOfViewer = await _context.UserBlocks
            .Where(b => b.BlockedUserId == viewerUserId)
            .Select(b => b.BlockerUserId)
            .ToListAsync(cancellationToken);

        var hidden = blockedByViewer.ToHashSet();
        hidden.UnionWith(blockersOfViewer);
        return hidden;
    }

    public async Task AddAsync(UserBlock block, CancellationToken cancellationToken = default) =>
        await _context.UserBlocks.AddAsync(block, cancellationToken);

    public async Task<IReadOnlyList<UserBlock>> GetBlocksByBlockerWithUsersAsync(
        int blockerUserId,
        CancellationToken cancellationToken = default) =>
        await _context.UserBlocks
            .Include(b => b.Blocked)
            .Where(b => b.BlockerUserId == blockerUserId)
            .OrderByDescending(b => b.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> RemoveAsync(int blockerUserId, int blockedUserId, CancellationToken cancellationToken = default)
    {
        var existing = await _context.UserBlocks.FirstOrDefaultAsync(
            b => b.BlockerUserId == blockerUserId && b.BlockedUserId == blockedUserId,
            cancellationToken);
        if (existing is null)
        {
            return false;
        }

        _context.UserBlocks.Remove(existing);
        return true;
    }
}
