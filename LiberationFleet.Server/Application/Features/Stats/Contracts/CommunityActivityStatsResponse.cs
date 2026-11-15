namespace LiberationFleet.Server.Application.Features.Stats.Contracts;

public sealed class CommunityActivityStatsResponse
{
    public int ActiveCrewCount { get; init; }
    public int ActiveCrewmateCount { get; init; }
    /// <summary>Rolling window used for the activity cutoff (days).</summary>
    public int ActiveWindowDays { get; init; }
}
