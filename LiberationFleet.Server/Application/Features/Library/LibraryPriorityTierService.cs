using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Library;

public class LibraryPriorityTierService(
    IMutualAidService mutualAidService,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IUserBlockRepository blockRepository)
{
    public async Task<LibraryPriorityTierSnapshot> GetSnapshotForCrewAsync(
        int homeCrewId,
        CancellationToken cancellationToken = default)
    {
        var crewIds = await GetAverageScopeCrewIdsAsync(homeCrewId, cancellationToken);
        return await BuildSnapshotForCrewIdsAsync(crewIds, cancellationToken);
    }

    /// <summary>
    /// Tier counts for home-crew members only (excluding the viewer and anyone hidden by blocks),
    /// using the same average as <see cref="GetSnapshotForCrewAsync"/>.
    /// </summary>
    public async Task<int[]> GetHomeCrewTierCountsForViewerAsync(
        int homeCrewId,
        int viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var scopeSnapshot = await GetSnapshotForCrewAsync(homeCrewId, cancellationToken);
        var audience = await GetVisibleAudienceMembershipsAsync(
            homeCrewId,
            viewerUserId,
            fleetWide: false,
            cancellationToken);
        return LibraryPriorityTier.BuildTierCounts(
            audience.Select(m => scopeSnapshot.GetTierForUser(m.UserId)));
    }

    /// <summary>
    /// Tier counts for the LoT average scope (fleet when in a fleet), excluding the viewer
    /// and anyone hidden by blocks.
    /// </summary>
    public async Task<int[]> GetScopeTierCountsForViewerAsync(
        int homeCrewId,
        int viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var scopeSnapshot = await GetSnapshotForCrewAsync(homeCrewId, cancellationToken);
        var audience = await GetVisibleAudienceMembershipsAsync(
            homeCrewId,
            viewerUserId,
            fleetWide: true,
            cancellationToken);
        return LibraryPriorityTier.BuildTierCounts(
            audience.Select(m => scopeSnapshot.GetTierForUser(m.UserId)));
    }

    /// <summary>
    /// Fleet-wide tier counts for a viewer, excluding the viewer and anyone hidden by blocks.
    /// </summary>
    public async Task<int[]> GetFleetTierCountsForViewerAsync(
        int fleetId,
        int viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotForFleetAsync(fleetId, cancellationToken);
        if (snapshot is null)
        {
            return LibraryPriorityTier.EmptyTierCounts();
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleetId, cancellationToken);
        var crewIds = fleetCrews.Select(fc => fc.CrewId).Distinct().ToList();
        if (crewIds.Count == 0)
        {
            return LibraryPriorityTier.EmptyTierCounts();
        }

        var audience = await CollectVisibleMembershipsAsync(
            crewIds,
            viewerUserId,
            cancellationToken);
        return LibraryPriorityTier.BuildTierCounts(
            audience.Select(m => snapshot.GetTierForUser(m.UserId)));
    }

    /// <summary>
    /// Visible non-blocked audience members for an offering visibility scope, with their LoT tiers.
    /// </summary>
    public async Task<IReadOnlyList<LibraryPriorityTierAudienceMember>> GetAudienceMembersForViewerAsync(
        int homeCrewId,
        int viewerUserId,
        bool fleetWide,
        CancellationToken cancellationToken = default)
    {
        var scopeSnapshot = await GetSnapshotForCrewAsync(homeCrewId, cancellationToken);
        var memberships = await GetVisibleAudienceMembershipsAsync(
            homeCrewId,
            viewerUserId,
            fleetWide,
            cancellationToken);

        return memberships
            .Select(m => new LibraryPriorityTierAudienceMember(
                m.UserId,
                m.User?.Username ?? $"User {m.UserId}",
                m.User?.AvatarResourceId,
                scopeSnapshot.GetTierForUser(m.UserId),
                m))
            .OrderBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<LibraryPriorityTierSummary> GetSummaryForUserAsync(
        int userId,
        int homeCrewId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetSnapshotForCrewAsync(homeCrewId, cancellationToken);
        return new LibraryPriorityTierSummary(
            snapshot.AverageScore,
            snapshot.GetTierForUser(userId),
            snapshot.TierCounts);
    }

    public async Task<LibraryPriorityTierSnapshot?> GetSnapshotForFleetAsync(
        int fleetId,
        CancellationToken cancellationToken = default)
    {
        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleetId, cancellationToken);
        var firstCrewId = fleetCrews.Select(fc => fc.CrewId).FirstOrDefault();
        if (firstCrewId == 0)
        {
            return null;
        }

        return await GetSnapshotForCrewAsync(firstCrewId, cancellationToken);
    }

    private async Task<IReadOnlyList<CrewMembership>> GetVisibleAudienceMembershipsAsync(
        int homeCrewId,
        int viewerUserId,
        bool fleetWide,
        CancellationToken cancellationToken)
    {
        // Fleet-wide with no fleet falls back to home crew only (same as average scope).
        var crewIds = fleetWide
            ? await GetAverageScopeCrewIdsAsync(homeCrewId, cancellationToken)
            : [homeCrewId];
        return await CollectVisibleMembershipsAsync(crewIds, viewerUserId, cancellationToken);
    }

    private async Task<IReadOnlyList<CrewMembership>> CollectVisibleMembershipsAsync(
        IReadOnlyList<int> crewIds,
        int viewerUserId,
        CancellationToken cancellationToken)
    {
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(
            viewerUserId,
            cancellationToken);
        var memberships = new List<CrewMembership>();
        var seen = new HashSet<int>();

        foreach (var crewId in crewIds)
        {
            var crewMembers = await membershipRepository.GetActiveMembersByCrewIdAsync(
                crewId,
                cancellationToken);
            foreach (var member in crewMembers)
            {
                if (member.IsPlaceholderMember)
                {
                    continue;
                }

                if (member.UserId == viewerUserId)
                {
                    continue;
                }

                if (hiddenUserIds.Contains(member.UserId))
                {
                    continue;
                }

                if (!seen.Add(member.UserId))
                {
                    continue;
                }

                memberships.Add(member);
            }
        }

        return memberships;
    }

    private async Task<LibraryPriorityTierSnapshot> BuildSnapshotForCrewIdsAsync(
        IReadOnlyList<int> crewIds,
        CancellationToken cancellationToken)
    {
        var members = new List<(int UserId, int HomeCrewId)>();
        foreach (var crewId in crewIds)
        {
            var crewMembers = await membershipRepository.GetActiveMembersByCrewIdAsync(crewId, cancellationToken);
            foreach (var member in crewMembers)
            {
                if (member.IsPlaceholderMember)
                {
                    continue;
                }

                members.Add((member.UserId, member.CrewId));
            }
        }

        if (members.Count == 0)
        {
            return new LibraryPriorityTierSnapshot(0m, LibraryPriorityTier.EmptyTierCounts(), new Dictionary<int, int>());
        }

        var scores = new Dictionary<int, decimal>();
        foreach (var (userId, crewId) in members)
        {
            if (scores.ContainsKey(userId))
            {
                continue;
            }

            var score = await mutualAidService.GetPriorityScoreForUserAsync(
                userId,
                crewId,
                cancellationToken,
                assumeInNeedNonOrganizerForLot: true);
            scores[userId] = score;
        }

        var average = scores.Count == 0 ? 0m : scores.Values.Average();
        var userTiers = scores.ToDictionary(
            pair => pair.Key,
            pair => LibraryPriorityTier.ResolveTier(pair.Value, average));
        var tierCounts = LibraryPriorityTier.BuildTierCounts(userTiers.Values);
        return new LibraryPriorityTierSnapshot(average, tierCounts, userTiers);
    }

    /// <summary>
    /// Fleet members when the crew is in a fleet; otherwise the home crew only.
    /// Independent of whether Library of Things is enabled.
    /// </summary>
    private async Task<IReadOnlyList<int>> GetAverageScopeCrewIdsAsync(
        int homeCrewId,
        CancellationToken cancellationToken)
    {
        var fleet = await fleetRepository.GetFleetForCrewAsync(homeCrewId, cancellationToken);
        if (fleet is null)
        {
            return [homeCrewId];
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var ids = fleetCrews.Select(fc => fc.CrewId).Distinct().ToList();
        if (ids.Count == 0)
        {
            return [homeCrewId];
        }

        if (!ids.Contains(homeCrewId))
        {
            ids.Add(homeCrewId);
        }

        return ids;
    }
}

public sealed record LibraryPriorityTierAudienceMember(
    int UserId,
    string Username,
    string? AvatarResourceId,
    int Tier,
    CrewMembership Membership);
