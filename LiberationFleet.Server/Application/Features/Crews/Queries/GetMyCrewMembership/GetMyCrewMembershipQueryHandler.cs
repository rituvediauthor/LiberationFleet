using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;

public class GetMyCrewMembershipQueryHandler(
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    IFleetRepository fleetRepository,
    ContentTenureService contentTenureService,
    ICurrentUserService currentUserService,
    ILogger<GetMyCrewMembershipQueryHandler> logger) : IRequestHandler<GetMyCrewMembershipQuery, CrewMembershipStatusDto>
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

        // Prefer the included navigation, but never treat a missing nav as "not in a crew"
        // when an active membership row exists.
        var crew = membership.Crew
            ?? await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            logger.LogWarning(
                "Active crew membership {MembershipId} for user {UserId} references missing crew {CrewId}.",
                membership.Id,
                userId.Value,
                membership.CrewId);
            return new CrewMembershipStatusDto { HasCrew = false };
        }

        // Gift stats power contribution-gated permissions only. Membership itself must not
        // fail closed to HasCrew=false when this query breaks (e.g. schema drift after audits).
        var giftStats = await TryGetGiftStatsAsync(
            userId.Value,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var lifetimeContributions = membership.LifetimeContributionOverride ?? giftStats.LifetimeContributions;
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
                lifetimeContributions,
                fleetTenureDays);
            canAttachFilesToFleetContent = FleetContentPermissionService.CanAttachFilesToFleetContent(
                fleet,
                membership,
                lifetimeContributions,
                fleetTenureDays);

            if (!canCreateFleetProposals && !membership.IsOrganizer && !membership.CanCreateProposals)
            {
                fleetProposalDaysRemaining = Math.Max(0, fleet.MinimumCrewmateTenureDaysForProposals - fleetTenureDays);
                fleetProposalContributionShortfall = Math.Max(
                    0m,
                    fleet.MinimumContributionForProposals - lifetimeContributions);
            }
        }

        var canCreateCrewProposals = CrewContentPermissionService.CanCreateProposals(
            crew,
            membership,
            lifetimeContributions,
            crewTenureDays);
        var crewProposalDaysRemaining = 0;
        var crewProposalContributionShortfall = 0m;
        if (!canCreateCrewProposals && !membership.IsOrganizer && !membership.CanCreateProposals)
        {
            crewProposalDaysRemaining = Math.Max(0, crew.MinimumCrewmateTenureDaysForProposals - crewTenureDays);
            crewProposalContributionShortfall = Math.Max(
                0m,
                crew.MinimumContributionForProposals - lifetimeContributions);
        }

        var pendingCycleThankYouGiftId = await giftRepository.GetPendingCycleThankYouGiftIdAsync(
            membership.CrewId,
            userId.Value,
            cancellationToken);

        return new CrewMembershipStatusDto
        {
            HasCrew = true,
            CrewId = membership.CrewId,
            CrewName = crew.Name,
            JoinCode = crew.JoinCode,
            LibraryOfThingsEnabled = crew.LibraryOfThingsEnabled,
            SeasonStarted = crew.SeasonStarted,
            IsInSeason = membership.IsInSeason,
            IsOrganizer = membership.IsOrganizer,
            ImageResourceId = crew.ImageResourceId,
            CanAttachFilesToCrewContent = CrewContentPermissionService.CanAttachFilesToCrewContent(
                crew,
                membership,
                lifetimeContributions,
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
            FleetProposalContributionShortfall = fleetProposalContributionShortfall,
            PendingCycleThankYouGiftId = pendingCycleThankYouGiftId
        };
    }

    private async Task<CrewmateGiftStatsDto> TryGetGiftStatsAsync(
        int userId,
        int crewId,
        DateTime? seasonStartDate,
        CancellationToken cancellationToken)
    {
        try
        {
            return await giftRepository.GetCrewmateGiftStatsAsync(
                userId,
                crewId,
                seasonStartDate,
                cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to load gift stats for membership status (user {UserId}, crew {CrewId}). Returning HasCrew with conservative contribution defaults.",
                userId,
                crewId);
            return new CrewmateGiftStatsDto();
        }
    }
}
