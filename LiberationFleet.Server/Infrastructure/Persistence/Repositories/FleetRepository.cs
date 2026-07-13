using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class FleetRepository : IFleetRepository
{
    private readonly ApplicationDbContext _context;

    public FleetRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Fleet?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Fleets.FirstOrDefaultAsync(f => f.Id == id, cancellationToken);

    public Task<Fleet?> GetByJoinCodeAsync(string joinCode, CancellationToken cancellationToken = default) =>
        _context.Fleets.FirstOrDefaultAsync(f => f.JoinCode == joinCode, cancellationToken);

    public async Task AddAsync(Fleet fleet, CancellationToken cancellationToken = default) =>
        await _context.Fleets.AddAsync(fleet, cancellationToken);

    public async Task<IReadOnlyList<Fleet>> SearchPublicAsync(CrewScope scope, CancellationToken cancellationToken = default) =>
        await _context.Fleets
            .Where(f => f.Privacy == CrewPrivacy.Public && f.Scope == scope)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FleetCrew>> GetFleetCrewsAsync(int fleetId, CancellationToken cancellationToken = default) =>
        await _context.FleetCrews
            .Include(fc => fc.Crew)
            .Where(fc => fc.FleetId == fleetId)
            .OrderBy(fc => fc.JoinedAt)
            .ToListAsync(cancellationToken);

    public async Task<Fleet?> GetFleetForCrewAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var fleetCrew = await _context.FleetCrews
            .Include(fc => fc.Fleet)
            .FirstOrDefaultAsync(fc => fc.CrewId == crewId, cancellationToken);
        return fleetCrew?.Fleet;
    }

    public Task<bool> IsCrewInFleetAsync(int crewId, int fleetId, CancellationToken cancellationToken = default) =>
        _context.FleetCrews.AnyAsync(fc => fc.CrewId == crewId && fc.FleetId == fleetId, cancellationToken);

    public async Task AddFleetCrewAsync(FleetCrew fleetCrew, CancellationToken cancellationToken = default) =>
        await _context.FleetCrews.AddAsync(fleetCrew, cancellationToken);

    public Task RemoveFleetCrewAsync(FleetCrew fleetCrew, CancellationToken cancellationToken = default)
    {
        _context.FleetCrews.Remove(fleetCrew);
        return Task.CompletedTask;
    }

    public Task<FleetCrew?> GetFleetCrewAsync(int fleetId, int crewId, CancellationToken cancellationToken = default) =>
        _context.FleetCrews.FirstOrDefaultAsync(fc => fc.FleetId == fleetId && fc.CrewId == crewId, cancellationToken);

    public Task<int> CountActiveFleetMembersAsync(int fleetId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.CountAsync(
            m => !m.IsBanned
                 && _context.FleetCrews.Any(fc => fc.FleetId == fleetId && fc.CrewId == m.CrewId),
            cancellationToken);

    public Task<bool> IsUserInFleetAsync(int userId, int fleetId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships.AnyAsync(
            m => m.UserId == userId
                 && !m.IsBanned
                 && _context.FleetCrews.Any(fc => fc.FleetId == fleetId && fc.CrewId == m.CrewId),
            cancellationToken);

    public async Task<IReadOnlyList<FleetRule>> GetPublicRulesAsync(int fleetId, CancellationToken cancellationToken = default) =>
        await _context.FleetRules
            .Where(r => r.FleetId == fleetId && r.IsPublic && !r.IsDeleted)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<FleetRule>> GetRulesAsync(int fleetId, CancellationToken cancellationToken = default) =>
        await _context.FleetRules
            .Include(r => r.CreatedByUser)
            .Where(r => r.FleetId == fleetId && !r.IsDeleted)
            .OrderBy(r => r.Id)
            .ToListAsync(cancellationToken);

    public Task<FleetRule?> GetRuleByIdAsync(int ruleId, CancellationToken cancellationToken = default) =>
        _context.FleetRules
            .Include(r => r.CreatedByUser)
            .FirstOrDefaultAsync(r => r.Id == ruleId && !r.IsDeleted, cancellationToken);

    public async Task AddRuleAsync(FleetRule rule, CancellationToken cancellationToken = default) =>
        await _context.FleetRules.AddAsync(rule, cancellationToken);

    public Task<ChatRoom?> GetLinkedFleetChatRoomAsync(int fleetId, int linkedCrewId, CancellationToken cancellationToken = default) =>
        _context.ChatRooms.FirstOrDefaultAsync(
            r => r.FleetId == fleetId && r.LinkedCrewId == linkedCrewId && !r.IsDeleted,
            cancellationToken);
}
