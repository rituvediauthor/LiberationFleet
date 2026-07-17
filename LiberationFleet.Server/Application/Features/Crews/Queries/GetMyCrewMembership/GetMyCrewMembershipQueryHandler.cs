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
        var fleetTenureDays = 0;
        var canCreateFleetProposals = false;
        var canAttachFilesToFleetContent = false;
        var fleetProposalDaysRemaining = 0;
        var fleetProposalContributionShortfall = 0m;
        if (fleet is not null)
        {
            fleetTenureDays = await contentTenureService.GetFleetTenureDaysAsync(
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

            if (!canCreateFleetProposals && !membership.IsOrganizer && !membership.CanCreateProposals)
            {
                fleetProposalDaysRemaining = Math.Max(0, fleet.MinimumCrewmateTenureDaysForProposals - fleetTenureDays);
                fleetProposalContributionShortfall = Math.Max(
                    0m,
                    fleet.MinimumContributionForProposals - giftStats.LifetimeContributions);
            }
        }

        var canCreateCrewProposals = CrewContentPermissionService.CanCreateProposals(
            crew,
            membership,
            giftStats.LifetimeContributions,
            crewTenureDays);
        var crewProposalDaysRemaining = 0;
        var crewProposalContributionShortfall = 0m;
        if (!canCreateCrewProposals && !membership.IsOrganizer && !membership.CanCreateProposals)
        {
            crewProposalDaysRemaining = Math.Max(0, crew.MinimumCrewmateTenureDaysForProposals - crewTenureDays);
            crewProposalContributionShortfall = Math.Max(
                0m,
                crew.MinimumContributionForProposals - giftStats.LifetimeContributions);
        }

        return new CrewMembershipStatusDto
        {
            HasCrew = true,
            CrewId = membership.CrewId,
            CrewName = crew.Name,
            JoinCode = crew.JoinCode,
            LibraryOfThingsEnabled = crew.LibraryOfThingsEnabled,
            IsOrganizer = membership.IsOrganizer,
            ImageResourceId = crew.ImageResourceId,
            CanAttachFilesToCrewContent = CrewContentPermissionService.CanAttachFilesToCrewContent(
                crew,
                membership,
                giftStats.LifetimeContributions,
                crewTenureDays),
            CanCreateProposals = canCreateCrewProposals,
            CanCreateFleetProposals = canCreateFleetProposals,
            CanAttachFilesToFleetContent = canAttachFilesToFleetContent,
            CanExportCrewData = CrewRoleAuthorizationService.CanExportCrewData(membership),
            CrewTenureDays = crewTenureDays,
            FleetTenureDays = fleetTenureDays,
            CrewProposalDaysRemaining = crewProposalDaysRemaining,
            CrewProposalContributionShortfall = crewProposalContributionShortfall,
            FleetProposalDaysRemaining = fleetProposalDaysRemaining,
            FleetProposalContributionShortfall = fleetProposalContributionShortfall
        };
    }
}
