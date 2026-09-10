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
    /// <summary>
    /// Crewmate tiers (4–6) among the home crew. Used for profile / crew dashboards.
    /// </summary>
    public async Task<LibraryPriorityTierSnapshot> GetCrewmateSnapshotForCrewAsync(
        int homeCrewId,
        CancellationToken cancellationToken = default)
    {
        var members = await GetScorableMembersAsync([homeCrewId], cancellationToken);
        return await BuildSnapshotAsync(members, LibraryPriorityTier.CrewmateMinTier, cancellationToken);
    }

    /// <summary>Backward-compatible alias for crewmate snapshot on the home crew.</summary>
    public Task<LibraryPriorityTierSnapshot> GetSnapshotForCrewAsync(
        int homeCrewId,
        CancellationToken cancellationToken = default) =>
        GetCrewmateSnapshotForCrewAsync(homeCrewId, cancellationToken);

    /// <summary>
    /// Viewer tier relative to an offering’s crew: 4–6 if crewmate, 1–3 if fleet-mate, else 1.
    /// </summary>
    public async Task<int> GetViewerTierForOfferingAsync(
        int viewerUserId,
        int offeringCrewId,
        CancellationToken cancellationToken = default)
    {
        var offeringMembers = await GetScorableMembersAsync([offeringCrewId], cancellationToken);
        if (offeringMembers.Any(m => m.UserId == viewerUserId))
        {
            var crewSnapshot = await BuildSnapshotAsync(
                offeringMembers,
                LibraryPriorityTier.CrewmateMinTier,
                cancellationToken);
            return crewSnapshot.GetTierForUser(viewerUserId);
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(offeringCrewId, cancellationToken);
        if (fleet is null)
        {
            return LibraryPriorityTier.MinTier;
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var otherCrewIds = fleetCrews
            .Select(fc => fc.CrewId)
            .Where(id => id != offeringCrewId)
            .Distinct()
            .ToList();
        if (otherCrewIds.Count == 0)
        {
            return LibraryPriorityTier.MinTier;
        }

        var fleetMateMembers = await GetScorableMembersAsync(otherCrewIds, cancellationToken);
        if (!fleetMateMembers.Any(m => m.UserId == viewerUserId))
        {
            return LibraryPriorityTier.MinTier;
        }

        var fleetSnapshot = await BuildSnapshotAsync(
            fleetMateMembers,
            LibraryPriorityTier.FleetMateMinTier,
            cancellationToken);
        return fleetSnapshot.GetTierForUser(viewerUserId);
    }

    /// <summary>
    /// Combined tier counts for an offering vendor crew: fleet-mates in 1–3, crewmates in 4–6.
    /// </summary>
    public async Task<int[]> GetOfferingAudienceTierCountsAsync(
        int offeringCrewId,
        CancellationToken cancellationToken = default)
    {
        var crewSnapshot = await GetCrewmateSnapshotForCrewAsync(offeringCrewId, cancellationToken);
        var counts = LibraryPriorityTier.EmptyTierCounts();
        for (var i = 0; i < counts.Length && i < crewSnapshot.TierCounts.Length; i++)
        {
            counts[i] = crewSnapshot.TierCounts[i];
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(offeringCrewId, cancellationToken);
        if (fleet is null)
        {
            return counts;
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var otherCrewIds = fleetCrews
            .Select(fc => fc.CrewId)
            .Where(id => id != offeringCrewId)
            .Distinct()
            .ToList();
        if (otherCrewIds.Count == 0)
        {
            return counts;
        }

        var fleetMates = await GetScorableMembersAsync(otherCrewIds, cancellationToken);
        var fleetSnapshot = await BuildSnapshotAsync(
            fleetMates,
            LibraryPriorityTier.FleetMateMinTier,
            cancellationToken);
        for (var i = 0; i < 3 && i < fleetSnapshot.TierCounts.Length; i++)
        {
            counts[i] = fleetSnapshot.TierCounts[i];
        }

        return counts;
    }

    public async Task<int[]> GetHomeCrewTierCountsForViewerAsync(
        int homeCrewId,
        int viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCrewmateSnapshotForCrewAsync(homeCrewId, cancellationToken);
        var audience = await GetVisibleAudienceMembershipsAsync(
            homeCrewId,
            viewerUserId,
            fleetWide: false,
            cancellationToken);
        return LibraryPriorityTier.BuildTierCounts(
            audience.Select(m => snapshot.GetTierForUser(m.UserId)));
    }

    public async Task<int[]> GetScopeTierCountsForViewerAsync(
        int homeCrewId,
        int viewerUserId,
        CancellationToken cancellationToken = default)
    {
        // Offering-audience distribution for the viewer’s home crew as vendor.
        _ = viewerUserId;
        return await GetOfferingAudienceTierCountsAsync(homeCrewId, cancellationToken);
    }

    public async Task<int[]> GetFleetTierCountsForViewerAsync(
        int fleetId,
        int viewerUserId,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(viewerUserId, cancellationToken);
        if (membership is null)
        {
            return LibraryPriorityTier.EmptyTierCounts();
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null || fleet.Id != fleetId)
        {
            return LibraryPriorityTier.EmptyTierCounts();
        }

        return await GetOfferingAudienceTierCountsAsync(membership.CrewId, cancellationToken);
    }

    public async Task<IReadOnlyList<LibraryPriorityTierAudienceMember>> GetAudienceMembersForViewerAsync(
        int homeCrewId,
        int viewerUserId,
        bool fleetWide,
        CancellationToken cancellationToken = default)
    {
        var members = new List<LibraryPriorityTierAudienceMember>();

        // Crewmates → tiers 4–6
        var crewMemberships = await GetVisibleAudienceMembershipsAsync(
            homeCrewId,
            viewerUserId,
            fleetWide: false,
            cancellationToken);
        var crewSnapshot = await GetCrewmateSnapshotForCrewAsync(homeCrewId, cancellationToken);
        foreach (var m in crewMemberships)
        {
            members.Add(new LibraryPriorityTierAudienceMember(
                m.UserId,
                m.User?.Username ?? $"User {m.UserId}",
                m.User?.AvatarResourceId,
                crewSnapshot.GetTierForUser(m.UserId),
                m));
        }

        if (!fleetWide)
        {
            return members
                .OrderBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(homeCrewId, cancellationToken);
        if (fleet is null)
        {
            return members
                .OrderBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
                .ToList();
        }

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        var otherCrewIds = fleetCrews
            .Select(fc => fc.CrewId)
            .Where(id => id != homeCrewId)
            .Distinct()
            .ToList();
        var fleetMateMemberships = await CollectVisibleMembershipsAsync(
            otherCrewIds,
            viewerUserId,
            cancellationToken);
        var fleetMateScores = await ScoreMembersAsync(fleetMateMemberships, cancellationToken);
        // Include scores for blocked-filtered audience only; tiers among all scorable fleet-mates.
        var allFleetMates = await GetScorableMembersAsync(otherCrewIds, cancellationToken);
        var fleetSnapshot = await BuildSnapshotAsync(
            allFleetMates,
            LibraryPriorityTier.FleetMateMinTier,
            cancellationToken);

        var seen = members.Select(m => m.UserId).ToHashSet();
        foreach (var m in fleetMateMemberships)
        {
            if (!seen.Add(m.UserId))
            {
                continue;
            }

            members.Add(new LibraryPriorityTierAudienceMember(
                m.UserId,
                m.User?.Username ?? $"User {m.UserId}",
                m.User?.AvatarResourceId,
                fleetSnapshot.GetTierForUser(m.UserId),
                m));
        }

        _ = fleetMateScores;
        return members
            .OrderBy(m => m.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    /// <summary>
    /// Profile / legacy summary: crewmate tier (4–6) within the home crew.
    /// </summary>
    public async Task<LibraryPriorityTierSummary> GetSummaryForUserAsync(
        int userId,
        int homeCrewId,
        CancellationToken cancellationToken = default)
    {
        var snapshot = await GetCrewmateSnapshotForCrewAsync(homeCrewId, cancellationToken);
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

        // Representational: offering-audience counts as if first crew were the vendor.
        var counts = await GetOfferingAudienceTierCountsAsync(firstCrewId, cancellationToken);
        var crewSnapshot = await GetCrewmateSnapshotForCrewAsync(firstCrewId, cancellationToken);
        return new LibraryPriorityTierSnapshot(crewSnapshot.AverageScore, counts, crewSnapshot.UserTiers);
    }

    private async Task<LibraryPriorityTierSnapshot> BuildSnapshotAsync(
        IReadOnlyList<CrewMembership> members,
        int tierBase,
        CancellationToken cancellationToken)
    {
        if (members.Count == 0)
        {
            return new LibraryPriorityTierSnapshot(
                0m,
                LibraryPriorityTier.EmptyTierCounts(),
                new Dictionary<int, int>());
        }

        var scores = await ScoreMembersAsync(members, cancellationToken);
        var average = scores.Count == 0 ? 0m : scores.Values.Average();
        var userTiers = LibraryPriorityTier.AssignTiers(scores, tierBase);
        var tierCounts = LibraryPriorityTier.BuildTierCounts(userTiers.Values);
        return new LibraryPriorityTierSnapshot(average, tierCounts, userTiers);
    }

    private async Task<Dictionary<int, decimal>> ScoreMembersAsync(
        IReadOnlyList<CrewMembership> members,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<int, decimal>();
        foreach (var member in members)
        {
            if (scores.ContainsKey(member.UserId))
            {
                continue;
            }

            var score = await mutualAidService.GetPriorityScoreForUserAsync(
                member.UserId,
                member.CrewId,
                cancellationToken,
                assumeInNeedNonOrganizerForLot: true);
            scores[member.UserId] = score;
        }

        return scores;
    }

    private async Task<IReadOnlyList<CrewMembership>> GetScorableMembersAsync(
        IReadOnlyList<int> crewIds,
        CancellationToken cancellationToken)
    {
        var members = new List<CrewMembership>();
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

                if (!seen.Add(member.UserId))
                {
                    continue;
                }

                members.Add(member);
            }
        }

        return members;
    }

    private async Task<IReadOnlyList<CrewMembership>> GetVisibleAudienceMembershipsAsync(
        int homeCrewId,
        int viewerUserId,
        bool fleetWide,
        CancellationToken cancellationToken)
    {
        var crewIds = fleetWide
            ? await GetFleetCrewIdsIncludingHomeAsync(homeCrewId, cancellationToken)
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

    private async Task<IReadOnlyList<int>> GetFleetCrewIdsIncludingHomeAsync(
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
