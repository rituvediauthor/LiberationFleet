using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;

public class GetMyCrewMembershipQueryHandler(
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    IFleetRepository fleetRepository,
    ContentTenureService contentTenureService,
    ICurrentUserService currentUserService) : IRequestHandler<GetMyCrewMembershipQuery, CrewMembershipStatusDto>
{
    public async Task<CrewMembershipStatusDto> Handle(GetMyCrewMembershipQuery request, CancellationToken cancellationToken)
    {
        var userId = currentUserService.UserId;
        if (userId is null)
        {
            return new CrewMembershipStatusDto();
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewMembershipStatusDto { HasCrew = false };
        }

        var crew = membership.Crew;
        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId.Value,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var crewTenureDays = await contentTenureService.GetCrewTenureDaysAsync(
            userId.Value,
            membership.CrewId,
            cancellationToken);

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        var canCreateFleetProposals = false;
        var canAttachFilesToFleetContent = false;
        if (fleet is not null)
        {
            var fleetTenureDays = await contentTenureService.GetFleetTenureDaysAsync(
                userId.Value,
                fleet.Id,
                cancellationToken);
            canCreateFleetProposals = FleetContentPermissionService.CanCreateProposals(
                fleet,
                membership,
                giftStats.LifetimeContributions,
                fleetTenureDays);
            canAttachFilesToFleetContent = FleetContentPermissionService.CanAttachFilesToFleetContent(
                fleet,
                membership,
                giftStats.LifetimeContributions,
                fleetTenureDays);
        }

        return new CrewMembershipStatusDto
        {
            HasCrew = true,
            CrewId = membership.CrewId,
            CrewName = crew.Name,
            JoinCode = crew.JoinCode,
            LibraryOfThingsEnabled = crew.LibraryOfThingsEnabled,
            CanAttachFilesToCrewContent = CrewContentPermissionService.CanAttachFilesToCrewContent(
                crew,
                membership,
                giftStats.LifetimeContributions,
                crewTenureDays),
            CanCreateProposals = CrewContentPermissionService.CanCreateProposals(
                crew,
                membership,
                giftStats.LifetimeContributions,
                crewTenureDays),
            CanCreateFleetProposals = canCreateFleetProposals,
            CanAttachFilesToFleetContent = canAttachFilesToFleetContent,
            CanExportCrewData = CrewRoleAuthorizationService.CanExportCrewData(membership)
        };
    }
}
