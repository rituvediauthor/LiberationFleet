using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IContentTenureRepository
{
    Task<UserCrewContentTenure?> GetCrewTenureAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task<UserFleetContentTenure?> GetFleetTenureAsync(int userId, int fleetId, CancellationToken cancellationToken = default);
    Task AddCrewTenureAsync(UserCrewContentTenure tenure, CancellationToken cancellationToken = default);
    Task AddFleetTenureAsync(UserFleetContentTenure tenure, CancellationToken cancellationToken = default);
}
