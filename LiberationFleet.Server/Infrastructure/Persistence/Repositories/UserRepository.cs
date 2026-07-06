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
