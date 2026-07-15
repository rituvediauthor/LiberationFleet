using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IAppDonationRepository
{
    Task AddAsync(AppDonation donation, CancellationToken cancellationToken = default);
    Task<AppDonation?> GetByStripeCheckoutSessionIdAsync(string sessionId, CancellationToken cancellationToken = default);
    Task<decimal> SumCompletedUsdForUserInRangeAsync(int userId, DateTime fromUtcInclusive, DateTime toUtcExclusive, CancellationToken cancellationToken = default);
}
