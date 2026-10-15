using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class DonationGoalRepository(ApplicationDbContext context) : IDonationGoalRepository
{
    public async Task<IReadOnlyList<DonationGoal>> GetAllOrderedAsync(CancellationToken cancellationToken = default) =>
        await context.DonationGoals
            .OrderBy(g => g.SortOrder)
            .ToListAsync(cancellationToken);
}
