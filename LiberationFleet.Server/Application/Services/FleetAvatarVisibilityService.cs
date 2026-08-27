using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

/// <summary>
/// Fleet surfaces may show a user's avatar only when that user can attach files to fleet content.
/// </summary>
public class FleetAvatarVisibilityService(
    IGiftRepository giftRepository,
    ContentTenureService contentTenureService)
{
    public static string? Filter(string? avatarResourceId, int authorUserId, IReadOnlySet<int>? allowedUserIds) =>
        CrewAvatarVisibilityService.Filter(avatarResourceId, authorUserId, allowedUserIds);

    public async Task<IReadOnlySet<int>> GetAllowedUserIdsAsync(
        Fleet fleet,
        IReadOnlyList<CrewMembership> members,
        CancellationToken cancellationToken = default)
    {
        var allowed = new HashSet<int>();
        foreach (var member in members)
        {
            if (await CanShowAvatarAsync(fleet, member, cancellationToken))
            {
                allowed.Add(member.UserId);
            }
        }

        return allowed;
    }

    public async Task<bool> CanShowAvatarAsync(
        Fleet fleet,
        CrewMembership membership,
        CancellationToken cancellationToken = default)
    {
        var crew = membership.Crew;
        if (crew is null)
        {
            return false;
        }

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            membership.UserId,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var tenureDays = await contentTenureService.GetFleetTenureDaysAsync(
            membership.UserId,
            fleet.Id,
            cancellationToken);
        var lifetime = membership.LifetimeContributionOverride ?? giftStats.LifetimeContributions;
        return FleetContentPermissionService.CanAttachFilesToFleetContent(
            fleet,
            membership,
            lifetime,
            tenureDays);
    }
}
