namespace LiberationFleet.Server.Application.Features.Gifts;

public static class SeasonProfileAccess
{
    public static bool CanEditEstimatedContribution(DateTime? givingSeasonJoinedAt) =>
        !givingSeasonJoinedAt.HasValue
        || DateTime.UtcNow - givingSeasonJoinedAt.Value < TimeSpan.FromDays(90);
}
