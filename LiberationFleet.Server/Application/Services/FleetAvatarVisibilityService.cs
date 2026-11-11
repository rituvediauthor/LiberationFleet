using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

/// <summary>
/// Fleet surfaces may show a user's avatar only when that user can attach files to fleet content.
/// </summary>
public class FleetAvatarVisibilityService(
    IMutualAidRepository mutualAidRepository,
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
        var needsStats = new List<CrewMembership>();

        foreach (var member in members)
        {
            if (member.Crew is null)
            {
                continue;
            }

            if (member.IsOrganizer || member.CanAttachFiles)
            {
                allowed.Add(member.UserId);
                continue;
            }

            if (member.AttachmentsDenied || !fleet.AllowCrewmateFileAttachments)
            {
                continue;
            }

            if (fleet.MinimumCrewmateTenureDaysForAttachments <= 0
                && fleet.MinimumContributionForAttachments <= 0m)
            {
                allowed.Add(member.UserId);
                continue;
            }

            needsStats.Add(member);
        }

        if (needsStats.Count == 0)
        {
            return allowed;
        }

        var needsTenure = fleet.MinimumCrewmateTenureDaysForAttachments > 0;
        var needsLifetime = fleet.MinimumContributionForAttachments > 0m;
        var userIdsNeedingLifetime = needsLifetime
            ? needsStats
                .Where(m => m.LifetimeContributionOverride is null)
                .Select(m => m.UserId)
                .Distinct()
                .ToList()
            : [];

        // Fleet tenure is shared; lifetime is still per home crew.
        var tenureByUser = needsTenure
            ? await contentTenureService.GetFleetTenureDaysForUsersAsync(
                needsStats.Select(m => m.UserId).Distinct().ToList(),
                fleet.Id,
                cancellationToken)
            : (IReadOnlyDictionary<int, int>)new Dictionary<int, int>();

        var lifetimeByCrew = new Dictionary<int, IReadOnlyDictionary<int, decimal>>();
        if (userIdsNeedingLifetime.Count > 0)
        {
            foreach (var crewGroup in needsStats
                .Where(m => m.LifetimeContributionOverride is null)
                .GroupBy(m => m.CrewId))
            {
                var crewUserIds = crewGroup.Select(m => m.UserId).Distinct().ToList();
                lifetimeByCrew[crewGroup.Key] = await mutualAidRepository.GetLifetimeContributionsForUsersAsync(
                    crewGroup.Key,
                    crewUserIds,
                    cancellationToken: cancellationToken);
            }
        }

        foreach (var member in needsStats)
        {
            var tenureDays = needsTenure ? tenureByUser.GetValueOrDefault(member.UserId) : 0;
            var lifetime = member.LifetimeContributionOverride
                ?? (needsLifetime
                    ? lifetimeByCrew.GetValueOrDefault(member.CrewId)?.GetValueOrDefault(member.UserId) ?? 0m
                    : 0m);

            if (FleetContentPermissionService.CanAttachFilesToFleetContent(
                    fleet,
                    member,
                    lifetime,
                    tenureDays))
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
        var allowed = await GetAllowedUserIdsAsync(fleet, [membership], cancellationToken);
        return allowed.Contains(membership.UserId);
    }
}
