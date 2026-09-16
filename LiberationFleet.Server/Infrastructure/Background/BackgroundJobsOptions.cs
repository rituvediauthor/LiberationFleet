namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// Controls whether maintenance jobs poll on a timer or only when related user traffic arrives.
/// Staging uses OnActivity so Azure SQL serverless can auto-pause while idle.
/// </summary>
public sealed class BackgroundJobsOptions
{
    public const string SectionName = "BackgroundJobs";

    /// <summary>Polling (default) or OnActivity.</summary>
    public BackgroundJobRunMode ProposalTimer { get; set; } = BackgroundJobRunMode.Polling;

    /// <summary>Polling (default) or OnActivity.</summary>
    public BackgroundJobRunMode GiftAutoVerify { get; set; } = BackgroundJobRunMode.Polling;

    /// <summary>Polling (default) or OnActivity.</summary>
    public BackgroundJobRunMode MediaDeepFreeze { get; set; } = BackgroundJobRunMode.Polling;

    /// <summary>When false, <see cref="ContentReportRetentionHostedService"/> does nothing.</summary>
    public bool ContentReportRetentionEnabled { get; set; } = true;

    /// <summary>
    /// Minimum gap between activity-triggered runs of the same job (avoids one page load
    /// stacking many sweeps while the user is already keeping SQL awake).
    /// </summary>
    public int ActivityDebounceSeconds { get; set; } = 120;
}

public enum BackgroundJobRunMode
{
    Polling = 0,
    OnActivity = 1
}
