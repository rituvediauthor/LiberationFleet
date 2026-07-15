using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class AppDonationRepository(ApplicationDbContext context) : IAppDonationRepository
{
    public async Task AddAsync(AppDonation donation, CancellationToken cancellationToken = default) =>
        await context.AppDonations.AddAsync(donation, cancellationToken);

    public Task<AppDonation?> GetByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken cancellationToken = default) =>
        context.AppDonations.FirstOrDefaultAsync(d => d.StripeCheckoutSessionId == sessionId, cancellationToken);

    public async Task<decimal> SumCompletedUsdForUserInRangeAsync(
        int userId,
        DateTime fromUtcInclusive,
        DateTime toUtcExclusive,
        CancellationToken cancellationToken = default)
    {
        var cents = await context.AppDonations
            .AsNoTracking()
            .Where(d => d.UserId == userId
                && d.Status == "completed"
                && d.CompletedAt != null
                && d.CompletedAt >= fromUtcInclusive
                && d.CompletedAt < toUtcExclusive)
            .SumAsync(d => (long?)d.AmountCents, cancellationToken) ?? 0L;

        return cents / 100m;
    }
}
