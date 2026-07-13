using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ICrewInvitationRepository
{
    Task AddAsync(CrewInvitation invitation, CancellationToken cancellationToken = default);
    Task<CrewInvitation?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<CrewInvitation?> GetPendingAsync(int crewId, int inviteeUserId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewInvitation>> GetPendingForInviteeAsync(int inviteeUserId, CancellationToken cancellationToken = default);
}

public interface IUserFleetRuleAcceptanceRepository
{
    Task<UserFleetRuleAcceptance?> GetAsync(int userId, int fleetId, CancellationToken cancellationToken = default);
    Task AddAsync(UserFleetRuleAcceptance acceptance, CancellationToken cancellationToken = default);
}
