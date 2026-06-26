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

    public async Task AddAsync(UserBlock block, CancellationToken cancellationToken = default) =>
        await _context.UserBlocks.AddAsync(block, cancellationToken);
}
