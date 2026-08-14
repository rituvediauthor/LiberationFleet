using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;

namespace LiberationFleet.Server.Application.Services;

public class CrewAvatarVisibilityService(
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ContentTenureService contentTenureService)
{
    public static string? Filter(string? avatarResourceId, int authorUserId, IReadOnlySet<int>? allowedUserIds)
    {
        if (string.IsNullOrWhiteSpace(avatarResourceId))
        {
            return null;
        }

        if (allowedUserIds is null || allowedUserIds.Contains(authorUserId))
        {
            return avatarResourceId;
        }

        return null;
    }

    public async Task<IReadOnlySet<int>> GetUsersAllowedToShowCrewAvatarAsync(
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        var allowed = new HashSet<int>();
        foreach (var member in members)
        {
            var resolvedCrew = member.Crew;
            if (resolvedCrew is null)
            {
                continue;
            }

            var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                member.UserId,
                crewId,
                resolvedCrew.CurrentSeasonStartDate,
                cancellationToken);
            var tenureDays = await contentTenureService.GetCrewTenureDaysAsync(
                member.UserId,
                crewId,
                cancellationToken);
            var lifetime = member.LifetimeContributionOverride ?? giftStats.LifetimeContributions;
            if (CrewContentPermissionService.CanAttachFilesToCrewContent(
                    resolvedCrew,
                    member,
                    lifetime,
                    tenureDays))
            {
                allowed.Add(member.UserId);
            }
        }

        return allowed;
    }
}
