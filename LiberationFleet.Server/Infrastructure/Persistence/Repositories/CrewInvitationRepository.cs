using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class CrewInvitationRepository(ApplicationDbContext context) : ICrewInvitationRepository
{
    public async Task AddAsync(CrewInvitation invitation, CancellationToken cancellationToken = default) =>
        await context.CrewInvitations.AddAsync(invitation, cancellationToken);

    public Task<CrewInvitation?> GetByIdAsync(int id, CancellationToken cancellationToken = default) =>
        context.CrewInvitations
            .Include(i => i.Crew)
            .Include(i => i.InviterUser)
            .Include(i => i.InviteeUser)
            .FirstOrDefaultAsync(i => i.Id == id, cancellationToken);

    public Task<CrewInvitation?> GetPendingAsync(int crewId, int inviteeUserId, CancellationToken cancellationToken = default) =>
        context.CrewInvitations.FirstOrDefaultAsync(
            i => i.CrewId == crewId
                && i.InviteeUserId == inviteeUserId
                && i.Status == CrewInvitationStatus.Pending,
            cancellationToken);

    public async Task<IReadOnlyList<CrewInvitation>> GetPendingForInviteeAsync(
        int inviteeUserId,
        CancellationToken cancellationToken = default) =>
        await context.CrewInvitations
            .Include(i => i.Crew)
            .Include(i => i.InviterUser)
            .Where(i => i.InviteeUserId == inviteeUserId && i.Status == CrewInvitationStatus.Pending)
            .OrderByDescending(i => i.CreatedAt)
            .ToListAsync(cancellationToken);
}

public class UserFleetRuleAcceptanceRepository(ApplicationDbContext context) : IUserFleetRuleAcceptanceRepository
{
    public Task<UserFleetRuleAcceptance?> GetAsync(int userId, int fleetId, CancellationToken cancellationToken = default) =>
        context.UserFleetRuleAcceptances.FirstOrDefaultAsync(
            a => a.UserId == userId && a.FleetId == fleetId,
            cancellationToken);

    public async Task AddAsync(UserFleetRuleAcceptance acceptance, CancellationToken cancellationToken = default) =>
        await context.UserFleetRuleAcceptances.AddAsync(acceptance, cancellationToken);
}
