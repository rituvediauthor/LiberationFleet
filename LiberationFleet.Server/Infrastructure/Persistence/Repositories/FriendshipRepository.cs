using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class FriendshipRepository : IFriendshipRepository
{
    private readonly ApplicationDbContext _context;

    public FriendshipRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Friendship?> GetBetweenUsersAsync(int userId, int otherUserId, CancellationToken cancellationToken = default) =>
        _context.Friendships.FirstOrDefaultAsync(
            f => (f.RequesterUserId == userId && f.AddresseeUserId == otherUserId)
                || (f.RequesterUserId == otherUserId && f.AddresseeUserId == userId),
            cancellationToken);

    public async Task<IReadOnlyList<Friendship>> GetForUserAsync(int userId, CancellationToken cancellationToken = default) =>
        await _context.Friendships
            .Where(f => f.RequesterUserId == userId || f.AddresseeUserId == userId)
            .ToListAsync(cancellationToken);

    public async Task AddAsync(Friendship friendship, CancellationToken cancellationToken = default) =>
        await _context.Friendships.AddAsync(friendship, cancellationToken);

    public void Remove(Friendship friendship) =>
        _context.Friendships.Remove(friendship);
}
