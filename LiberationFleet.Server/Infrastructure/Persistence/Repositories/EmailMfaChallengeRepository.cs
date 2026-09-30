using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class EmailMfaChallengeRepository(ApplicationDbContext context) : IEmailMfaChallengeRepository
{
    public Task<EmailMfaChallenge?> GetActiveByTokenAsync(string challengeToken, CancellationToken cancellationToken = default) =>
        context.EmailMfaChallenges
            .Include(c => c.User)
            .FirstOrDefaultAsync(
                c => c.ChallengeToken == challengeToken
                    && c.ConsumedAt == null
                    && c.ExpiresAt > DateTime.UtcNow,
                cancellationToken);

    public async Task InvalidateOpenAsync(int userId, EmailMfaPurpose purpose, CancellationToken cancellationToken = default)
    {
        var open = await context.EmailMfaChallenges
            .Where(c => c.UserId == userId && c.Purpose == purpose && c.ConsumedAt == null)
            .ToListAsync(cancellationToken);

        var now = DateTime.UtcNow;
        foreach (var challenge in open)
        {
            challenge.ConsumedAt = now;
        }
    }

    public async Task AddAsync(EmailMfaChallenge challenge, CancellationToken cancellationToken = default) =>
        await context.EmailMfaChallenges.AddAsync(challenge, cancellationToken);

    public Task UpdateAsync(EmailMfaChallenge challenge, CancellationToken cancellationToken = default)
    {
        context.EmailMfaChallenges.Update(challenge);
        return Task.CompletedTask;
    }
}
