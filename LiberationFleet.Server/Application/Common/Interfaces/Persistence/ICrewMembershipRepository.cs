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
    /// <summary>
    /// Reactivate a soft-left (non-banned) membership for this crew, preserving crew-specific
    /// stats/roles, or create a new membership when none exists.
    /// </summary>
    Task<CrewMembership> ReactivateOrCreateAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task AddAsync(CrewMembership membership, CancellationToken cancellationToken = default);
    void Remove(CrewMembership membership);
    /// <summary>Soft-leave: retain the row and crew-specific stats for a later rejoin.</summary>
    void MarkLeft(CrewMembership membership, DateTime leftAtUtc);
}
