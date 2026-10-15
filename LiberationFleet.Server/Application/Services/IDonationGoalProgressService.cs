namespace LiberationFleet.Server.Application.Services;

public interface IDonationGoalProgressService
{
    /// <summary>Apply November cycle transitions, then return the public current-goal view.</summary>
    Task<DonationGoalCurrentDto> GetCurrentAsync(
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default);

    /// <summary>
    /// Credit a newly completed donation to the active goal, cascading excess.
    /// Caller is responsible for <c>SaveChanges</c>.
    /// </summary>
    Task ApplyDonationAsync(
        long amountCents,
        DateTime? utcNow = null,
        CancellationToken cancellationToken = default);
}

public class DonationGoalCurrentDto
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int RaisedUsd { get; set; }
    public int GoalUsd { get; set; }
    public string ProgressLabel { get; set; } = string.Empty;
    public int Percent { get; set; }
    public string? NextGoalTitle { get; set; }
}
