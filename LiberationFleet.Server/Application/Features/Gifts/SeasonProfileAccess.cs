namespace LiberationFleet.Server.Application.Features.Gifts;

public static class SeasonProfileAccess
{
    public static bool CanEditEstimatedContribution(
        DateTime? givingSeasonJoinedAt,
        DateTime? utcNow = null)
    {
        var now = utcNow ?? DateTime.UtcNow;
        return !givingSeasonJoinedAt.HasValue
            || now - givingSeasonJoinedAt.Value < TimeSpan.FromDays(90);
    }
}
