using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IDonationGoalRepository
{
    Task<IReadOnlyList<DonationGoal>> GetAllOrderedAsync(CancellationToken cancellationToken = default);
}
