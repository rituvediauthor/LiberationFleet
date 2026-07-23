using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Fleets;

/// <summary>
/// Manages No-Crew fleet membership: stay in the fleet after leaving/being kicked from a crew,
/// and auto-attach a newly created crew to that fleet.
/// </summary>
public class FleetMembershipService(
    IFleetRepository fleetRepository,
    ContentTenureService contentTenureService)
{
    public async Task RetainInFleetAsNoCrewAsync(
        int userId,
        int departedCrewId,
        CancellationToken cancellationToken = default)
    {
        var fleet = await fleetRepository.GetFleetForCrewAsync(departedCrewId, cancellationToken);
        if (fleet is null)
        {
            await contentTenureService.OnLeftCrewAsync(userId, departedCrewId, cancellationToken);
            return;
        }

        // Stay in the fleet: pause crew tenure only; keep fleet tenure running.
        await contentTenureService.PauseCrewAsync(userId, departedCrewId, cancellationToken);
        await fleetRepository.EnsureFleetMembershipAsync(userId, fleet.Id, cancellationToken);
    }

    public async Task AttachCreatedCrewToFleetIfNeededAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var membership = await fleetRepository.GetFleetMembershipForUserAsync(userId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        var existingFleet = await fleetRepository.GetFleetForCrewAsync(crewId, cancellationToken);
        if (existingFleet is not null)
        {
            await fleetRepository.RemoveFleetMembershipAsync(membership, cancellationToken);
            return;
        }

        await fleetRepository.AddFleetCrewAsync(new FleetCrew
        {
            FleetId = membership.FleetId,
            CrewId = crewId,
            JoinedAt = DateTime.UtcNow
        }, cancellationToken);

        await contentTenureService.OnCrewJoinedFleetAsync(crewId, membership.FleetId, cancellationToken);
        await fleetRepository.RemoveFleetMembershipAsync(membership, cancellationToken);
    }

    public Task ClearFleetMembershipAsync(int userId, int fleetId, CancellationToken cancellationToken = default) =>
        fleetRepository.RemoveFleetMembershipForUserAsync(userId, fleetId, cancellationToken);
}
