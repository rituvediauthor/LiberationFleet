using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IFleetRepository
{
    Task<Fleet?> GetByIdAsync(int id, CancellationToken cancellationToken = default);
    Task<Fleet?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default);
    Task AddAsync(Fleet fleet, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Fleet>> SearchPublicAsync(CrewScope scope, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FleetCrew>> GetFleetCrewsAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<Fleet?> GetFleetForCrewAsync(int crewId, CancellationToken cancellationToken = default);
    Task<bool> IsCrewInFleetAsync(int crewId, int fleetId, CancellationToken cancellationToken = default);
    Task AddFleetCrewAsync(FleetCrew fleetCrew, CancellationToken cancellationToken = default);
    Task RemoveFleetCrewAsync(FleetCrew fleetCrew, CancellationToken cancellationToken = default);
    Task<FleetCrew?> GetFleetCrewAsync(int fleetId, int crewId, CancellationToken cancellationToken = default);
    Task<int> CountActiveFleetMembersAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<bool> IsUserInFleetAsync(int userId, int fleetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<int>> GetActiveFleetMemberUserIdsAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FleetRule>> GetPublicRulesAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<FleetRule>> GetRulesAsync(int fleetId, CancellationToken cancellationToken = default);
    Task<FleetRule?> GetRuleByIdAsync(int ruleId, CancellationToken cancellationToken = default);
    Task AddRuleAsync(FleetRule rule, CancellationToken cancellationToken = default);
    Task<ChatRoom?> GetLinkedFleetChatRoomAsync(int fleetId, int linkedCrewId, CancellationToken cancellationToken = default);
}
