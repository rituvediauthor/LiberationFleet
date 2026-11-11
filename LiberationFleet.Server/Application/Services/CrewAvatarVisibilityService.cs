using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public class CrewAvatarVisibilityService(
    ICrewMembershipRepository membershipRepository,
    IMutualAidRepository mutualAidRepository,
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

    public Task<IReadOnlySet<int>> GetUsersAllowedToShowCrewAvatarAsync(
        int crewId,
        CancellationToken cancellationToken = default) =>
        GetUsersAllowedToShowCrewAvatarAsync(crewId, members: null, cancellationToken);

    /// <summary>
    /// Prefer passing already-loaded active members to avoid a second roster query.
    /// </summary>
    public async Task<IReadOnlySet<int>> GetUsersAllowedToShowCrewAvatarAsync(
        int crewId,
        IReadOnlyList<CrewMembership>? members,
        CancellationToken cancellationToken = default)
    {
        members ??= await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
        var allowed = new HashSet<int>();
        var needsStats = new List<CrewMembership>();

        foreach (var member in members)
        {
            var crew = member.Crew;
            if (crew is null)
            {
                continue;
            }

            // Mirror CanAttachFilesToCrewContent short-circuits before any gift/tenure IO.
            if (member.IsOrganizer || member.CanAttachFiles)
            {
                allowed.Add(member.UserId);
                continue;
            }

            if (member.AttachmentsDenied || !crew.AllowCrewmateFileAttachments)
            {
                continue;
            }

            if (crew.MinimumCrewmateTenureDaysForAttachments <= 0
                && crew.MinimumContributionForAttachments <= 0m)
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

        var needsTenure = needsStats
            .Where(m => m.Crew!.MinimumCrewmateTenureDaysForAttachments > 0)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();
        var needsLifetime = needsStats
            .Where(m => m.Crew!.MinimumContributionForAttachments > 0m
                && m.LifetimeContributionOverride is null)
            .Select(m => m.UserId)
            .Distinct()
            .ToList();

        var tenureByUser = needsTenure.Count == 0
            ? (IReadOnlyDictionary<int, int>)new Dictionary<int, int>()
            : await contentTenureService.GetCrewTenureDaysForUsersAsync(needsTenure, crewId, cancellationToken);
        var lifetimeByUser = needsLifetime.Count == 0
            ? (IReadOnlyDictionary<int, decimal>)new Dictionary<int, decimal>()
            : await mutualAidRepository.GetLifetimeContributionsForUsersAsync(
                crewId,
                needsLifetime,
                cancellationToken: cancellationToken);

        foreach (var member in needsStats)
        {
            var crew = member.Crew!;
            var tenureDays = crew.MinimumCrewmateTenureDaysForAttachments <= 0
                ? 0
                : tenureByUser.GetValueOrDefault(member.UserId);
            var lifetime = member.LifetimeContributionOverride
                ?? (crew.MinimumContributionForAttachments <= 0m
                    ? 0m
                    : lifetimeByUser.GetValueOrDefault(member.UserId));

            if (CrewContentPermissionService.CanAttachFilesToCrewContent(
                    crew,
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
