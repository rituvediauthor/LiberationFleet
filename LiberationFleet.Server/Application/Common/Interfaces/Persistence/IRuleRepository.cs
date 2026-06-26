using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IRuleRepository
{
    Task<CrewRule?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CrewRule?> GetByIdWithAuthorAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewRule>> GetByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task AddAsync(CrewRule rule, CancellationToken cancellationToken = default);
}
