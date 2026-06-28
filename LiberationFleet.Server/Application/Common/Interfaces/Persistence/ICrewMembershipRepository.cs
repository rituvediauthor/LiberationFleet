using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ICrewMembershipRepository
{
    Task<CrewMembership?> GetActiveMembershipAsync(int userId, CancellationToken cancellationToken = default);
    Task<bool> IsUserBannedFromCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task<bool> IsUserInCrewAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetActiveMembersByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetBannedMembersByCrewIdAsync(int crewId, CancellationToken cancellationToken = default);
    Task<CrewMembership?> GetMembershipAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task AddAsync(CrewMembership membership, CancellationToken cancellationToken = default);
    void Remove(CrewMembership membership);
}
