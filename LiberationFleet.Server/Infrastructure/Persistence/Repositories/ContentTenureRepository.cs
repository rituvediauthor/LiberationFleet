using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class ContentTenureRepository(ApplicationDbContext context) : IContentTenureRepository
{
    public Task<UserCrewContentTenure?> GetCrewTenureAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        // Tracked: callers such as Resume/Pause mutate AccruedTicks / ClockStartedAtUtc.
        context.UserCrewContentTenures.FirstOrDefaultAsync(
            t => t.UserId == userId && t.CrewId == crewId,
            cancellationToken);

    public async Task<IReadOnlyList<UserCrewContentTenure>> GetCrewTenuresForUsersAsync(
        IReadOnlyCollection<int> userIds,
        int crewId,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<UserCrewContentTenure>();
        }

        var ids = userIds as IList<int> ?? userIds.ToList();
        return await context.UserCrewContentTenures
            .AsNoTracking()
            .Where(t => t.CrewId == crewId && ids.Contains(t.UserId))
            .ToListAsync(cancellationToken);
    }

    public Task<UserFleetContentTenure?> GetFleetTenureAsync(
        int userId,
        int fleetId,
        CancellationToken cancellationToken = default) =>
        // Tracked: callers such as Resume/Pause mutate AccruedTicks / ClockStartedAtUtc.
        context.UserFleetContentTenures.FirstOrDefaultAsync(
            t => t.UserId == userId && t.FleetId == fleetId,
            cancellationToken);

    public async Task<IReadOnlyList<UserFleetContentTenure>> GetFleetTenuresForUsersAsync(
        IReadOnlyCollection<int> userIds,
        int fleetId,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return Array.Empty<UserFleetContentTenure>();
        }

        var ids = userIds as IList<int> ?? userIds.ToList();
        return await context.UserFleetContentTenures
            .AsNoTracking()
            .Where(t => t.FleetId == fleetId && ids.Contains(t.UserId))
            .ToListAsync(cancellationToken);
    }

    public async Task AddCrewTenureAsync(UserCrewContentTenure tenure, CancellationToken cancellationToken = default) =>
        await context.UserCrewContentTenures.AddAsync(tenure, cancellationToken);

    public async Task AddFleetTenureAsync(UserFleetContentTenure tenure, CancellationToken cancellationToken = default) =>
        await context.UserFleetContentTenures.AddAsync(tenure, cancellationToken);
}
