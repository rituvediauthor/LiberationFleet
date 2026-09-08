namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// Maps a LoT priority score to tiers 1–5 relative to the fleet/crew average.
/// </summary>
public static class LibraryPriorityTier
{
    public const int MinTier = 1;
    public const int MaxTier = 5;

    public static int ClampTier(int tier) =>
        Math.Clamp(tier, MinTier, MaxTier);

    /// <summary>
    /// Tier 1: score 0, average 0, or ratio ≤ 0.20.
    /// Tier 2–4: successive 20% bands.
    /// Tier 5: ratio &gt; 0.80 (includes above-average).
    /// </summary>
    public static int ResolveTier(decimal score, decimal average)
    {
        if (score <= 0m || average <= 0m)
        {
            return 1;
        }

        var ratio = score / average;
        if (ratio <= 0.20m)
        {
            return 1;
        }

        if (ratio <= 0.40m)
        {
            return 2;
        }

        if (ratio <= 0.60m)
        {
            return 3;
        }

        if (ratio <= 0.80m)
        {
            return 4;
        }

        return 5;
    }

    public static int[] EmptyTierCounts() => [0, 0, 0, 0, 0];

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
        UserTiers.TryGetValue(userId, out var tier) ? tier : 1;
}

public sealed record LibraryPriorityTierSummary(
    decimal AverageScore,
    int ViewerTier,
    int[] TierCounts);
