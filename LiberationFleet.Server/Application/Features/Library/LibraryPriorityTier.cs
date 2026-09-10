namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// LoT priority tiers 1–6.
/// Tiers 1–3 are for fleet-mates (same fleet, not the offering’s crew), ranked by tercile
/// among fleet members excluding the offering crew.
/// Tiers 4–6 are for crewmates of the offering’s crew, ranked by tercile within that crew.
/// Higher score → higher tercile → higher tier number.
/// </summary>
public static class LibraryPriorityTier
{
    public const int MinTier = 1;
    public const int MaxTier = 6;

    public const int FleetMateMinTier = 1;
    public const int FleetMateMaxTier = 3;
    public const int CrewmateMinTier = 4;
    public const int CrewmateMaxTier = 6;

    public static int ClampTier(int tier) =>
        Math.Clamp(tier, MinTier, MaxTier);

    public static bool IsFleetMateTier(int tier) =>
        tier is >= FleetMateMinTier and <= FleetMateMaxTier;

    public static bool IsCrewmateTier(int tier) =>
        tier is >= CrewmateMinTier and <= CrewmateMaxTier;

    /// <summary>
    /// Maps a 0-based index in an ascending score list to tercile band 0 (low), 1 (mid), or 2 (high).
    /// Sole member is treated as the top third.
    /// </summary>
    public static int ResolveTercileBandFromIndex(int zeroBasedIndex, int count)
    {
        if (count <= 0)
        {
            return 2;
        }

        if (count == 1)
        {
            return 2;
        }

        var index = Math.Clamp(zeroBasedIndex, 0, count - 1);
        var bottomSize = (count + 2) / 3;
        var middleSize = (count + 1) / 3;
        if (index < bottomSize)
        {
            return 0;
        }

        if (index < bottomSize + middleSize)
        {
            return 1;
        }

        return 2;
    }

    /// <summary>
    /// Assign tiers for a peer group. <paramref name="tierBase"/> is 1 (fleet-mates) or 4 (crewmates).
    /// </summary>
    public static IReadOnlyDictionary<int, int> AssignTiers(
        IReadOnlyDictionary<int, decimal> userScores,
        int tierBase)
    {
        if (tierBase is not (FleetMateMinTier or CrewmateMinTier))
        {
            throw new ArgumentOutOfRangeException(nameof(tierBase), tierBase, "tierBase must be 1 or 4.");
        }

        if (userScores.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        var ordered = userScores
            .OrderBy(pair => pair.Value)
            .ThenBy(pair => pair.Key)
            .ToList();

        var result = new Dictionary<int, int>(ordered.Count);
        for (var i = 0; i < ordered.Count; i++)
        {
            var band = ResolveTercileBandFromIndex(i, ordered.Count);
            result[ordered[i].Key] = tierBase + band;
        }

        return result;
    }

    public static int[] EmptyTierCounts() => [0, 0, 0, 0, 0, 0];

    public static int[] BuildTierCounts(IEnumerable<int> tiers)
    {
        var counts = EmptyTierCounts();
        foreach (var tier in tiers)
        {
            var clamped = ClampTier(tier);
            counts[clamped - 1]++;
        }

        return counts;
    }
}

public sealed record LibraryPriorityTierSnapshot(
    decimal AverageScore,
    int[] TierCounts,
    IReadOnlyDictionary<int, int> UserTiers)
{
    public int GetTierForUser(int userId) =>
        UserTiers.TryGetValue(userId, out var tier) ? tier : LibraryPriorityTier.MinTier;
}

public sealed record LibraryPriorityTierSummary(
    decimal AverageScore,
    int ViewerTier,
    int[] TierCounts);
