using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ICrewRepository
{
    Task<Crew?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Crew?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task AddAsync(Crew crew, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Crew>> SearchPublicAsync(CrewScope scope, CancellationToken cancellationToken = default);
    Task<int> CountMembersAsync(int crewId, CancellationToken cancellationToken = default);
}
