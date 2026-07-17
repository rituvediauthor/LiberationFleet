using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class UserRepository : IUserRepository
{
    private readonly ApplicationDbContext _context;

    public UserRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<bool> ExistsByEmailOrUsernameAsync(string email, string username, CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(
            u => u.Email == email || u.Username == username,
            cancellationToken);
    }

    public Task<User?> GetByEmailOrUsernameAsync(string emailOrUsername, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(
            u => u.Email == emailOrUsername || u.Username == emailOrUsername,
            cancellationToken);
    }

    public Task<User?> GetByEmailAsync(string email, CancellationToken cancellationToken = default)
    {
        return _context.Users.FirstOrDefaultAsync(u => u.Email == email, cancellationToken);
    }

    public Task<User?> GetByIdWithProfileAsync(int id, CancellationToken cancellationToken = default)
    {
        return _context.Users
            .Include(u => u.PaymentPlatforms)
                .ThenInclude(p => p.CrewPaymentPlatform)
            .FirstOrDefaultAsync(u => u.Id == id, cancellationToken);
    }

    public async Task<IReadOnlyDictionary<int, string?>> GetAvatarResourceIdsAsync(
        IReadOnlyList<int> userIds,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<int, string?>();
        }

        var distinctIds = userIds.Distinct().ToList();
        return await _context.Users
            .AsNoTracking()
            .Where(u => distinctIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, u => u.AvatarResourceId, cancellationToken);
    }

    public async Task<IReadOnlyList<User>> SearchByUsernameAsync(
        string usernameFragment,
        int limit = 20,
        CancellationToken cancellationToken = default)
    {
        var term = usernameFragment.Trim();
        if (term.Length < 2)
        {
            return Array.Empty<User>();
        }

        return await _context.Users
            .Where(u => !u.IsUnclaimedPlaceholder && u.Username.Contains(term))
            .OrderBy(u => u.Username)
            .Take(Math.Clamp(limit, 1, 50))
            .ToListAsync(cancellationToken);
    }

    public Task<bool> IsUsernameTakenByOtherUserAsync(string username, int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(
            u => u.Username == username && u.Id != userId,
            cancellationToken);
    }

    public Task<bool> IsEmailTakenByOtherUserAsync(string email, int userId, CancellationToken cancellationToken = default)
    {
        return _context.Users.AnyAsync(
            u => u.Email == email && u.Id != userId,
            cancellationToken);
    }

    public async Task AddAsync(User user, CancellationToken cancellationToken = default)
    {
        await _context.Users.AddAsync(user, cancellationToken);
    }

    public Task UpdateAsync(User user, CancellationToken cancellationToken = default)
    {
        if (_context.Entry(user).State == EntityState.Detached)
        {
            _context.Users.Update(user);
        }

        return Task.CompletedTask;
    }

    public void Remove(User user)
    {
        _context.Users.Remove(user);
    }
}
