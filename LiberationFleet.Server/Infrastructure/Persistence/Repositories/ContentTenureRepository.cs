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
        context.UserCrewContentTenures.FirstOrDefaultAsync(
            t => t.UserId == userId && t.CrewId == crewId,
            cancellationToken);

    public Task<UserFleetContentTenure?> GetFleetTenureAsync(
        int userId,
        int fleetId,
        CancellationToken cancellationToken = default) =>
        context.UserFleetContentTenures.FirstOrDefaultAsync(
            t => t.UserId == userId && t.FleetId == fleetId,
            cancellationToken);

    public async Task AddCrewTenureAsync(UserCrewContentTenure tenure, CancellationToken cancellationToken = default) =>
        await context.UserCrewContentTenures.AddAsync(tenure, cancellationToken);

    public async Task AddFleetTenureAsync(UserFleetContentTenure tenure, CancellationToken cancellationToken = default) =>
        await context.UserFleetContentTenures.AddAsync(tenure, cancellationToken);
}
