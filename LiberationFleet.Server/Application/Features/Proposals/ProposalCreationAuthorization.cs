using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Proposals;

/// <summary>
/// Gates creation of proposals (General and system) behind CanCreateProposals / tenure rules.
/// Outsider join requests are exempt — the applicant is not yet a member.
/// </summary>
public static class ProposalCreationAuthorization
{
    public static async Task<(bool Allowed, string? Error)> EnsureCrewMemberCanCreateAsync(
        Crew crew,
        CrewMembership membership,
        IGiftRepository giftRepository,
        ContentTenureService contentTenureService,
        CancellationToken cancellationToken)
    {
        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            membership.UserId,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var tenureDays = await contentTenureService.GetCrewTenureDaysAsync(
            membership.UserId,
            membership.CrewId,
            cancellationToken);

        if (CrewContentPermissionService.CanCreateProposals(
                crew,
                membership,
                giftStats.LifetimeContributions,
                tenureDays))
        {
            return (true, null);
        }

        return (false, "You are not allowed to create proposals yet.");
    }

    public static async Task<(bool Allowed, string? Error)> EnsureFleetMemberCanCreateAsync(
        Domain.Entities.Fleet fleet,
        CrewMembership membership,
        Crew? crew,
        IGiftRepository giftRepository,
        ContentTenureService contentTenureService,
        CancellationToken cancellationToken)
    {
        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            membership.UserId,
            membership.CrewId,
            crew?.CurrentSeasonStartDate,
            cancellationToken);
        var tenureDays = await contentTenureService.GetFleetTenureDaysAsync(
            membership.UserId,
            fleet.Id,
            cancellationToken);

        if (FleetContentPermissionService.CanCreateProposals(
                fleet,
                membership,
                giftStats.LifetimeContributions,
                tenureDays))
        {
            return (true, null);
        }

        return (false, "You are not allowed to create fleet proposals yet.");
    }
}
