using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class PasswordResetTokenRepository : IPasswordResetTokenRepository
{
    private readonly ApplicationDbContext _context;

    public PasswordResetTokenRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<PasswordResetToken?> GetActiveByTokenAsync(string token, CancellationToken cancellationToken = default)
    {
        return _context.PasswordResetTokens
            .Include(rt => rt.User)
            .FirstOrDefaultAsync(rt => rt.Token == token && !rt.IsUsed, cancellationToken);
    }

    public async Task AddAsync(PasswordResetToken resetToken, CancellationToken cancellationToken = default)
    {
        await _context.PasswordResetTokens.AddAsync(resetToken, cancellationToken);
    }

    public Task UpdateAsync(PasswordResetToken resetToken, CancellationToken cancellationToken = default)
    {
        _context.PasswordResetTokens.Update(resetToken);
        return Task.CompletedTask;
    }
}
