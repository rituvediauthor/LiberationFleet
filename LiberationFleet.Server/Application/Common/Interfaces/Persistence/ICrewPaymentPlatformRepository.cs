using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ICrewPaymentPlatformRepository
{
    Task<IReadOnlyList<CrewPaymentPlatform>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewPaymentPlatform>> GetUsedByOtherCrewmatesAsync(int crewId, int userId, CancellationToken cancellationToken = default);
    Task<CrewPaymentPlatform?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CrewPaymentPlatform?> GetByCrewAndNameAsync(int crewId, string name, CancellationToken cancellationToken = default);
    Task<CrewPaymentPlatform> AddAsync(CrewPaymentPlatform platform, CancellationToken cancellationToken = default);
    Task<bool> ExistsForCrewAsync(int crewId, int platformId, CancellationToken cancellationToken = default);
}
