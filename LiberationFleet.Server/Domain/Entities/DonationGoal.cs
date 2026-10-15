using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Platform fundraising goal. Progress is credited from completed Stripe donations.
/// </summary>
public class DonationGoal
{
    public int Id { get; set; }

    /// <summary>Stable catalog key (e.g. launch-app-store).</summary>
    public string Key { get; set; } = string.Empty;

    public string Title { get; set; } = string.Empty;

    /// <summary>
    /// Description body. For <see cref="DonationGoalKeys.FundTeammate"/>, use
    /// <c>{completions}</c> as a placeholder for CompletionsThisYear.
    /// </summary>
    public string Description { get; set; } = string.Empty;

    public long TargetCents { get; set; }
    public long ProgressCents { get; set; }
    public int SortOrder { get; set; }
    public DonationGoalRecurrence Recurrence { get; set; }

    /// <summary>One-time / current annual instance completion.</summary>
    public bool IsCompleted { get; set; }

    /// <summary>Teammate goal only — resets each November cycle.</summary>
    public int CompletionsThisYear { get; set; }

    /// <summary>
    /// Nov 1 (date-only, Eastern calendar) of the annual cycle last applied.
    /// Used so November resets are idempotent.
    /// </summary>
    public DateOnly? AnnualYearStart { get; set; }

    public DateTime? CompletedAt { get; set; }
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
}

public static class DonationGoalKeys
{
    public const string LaunchAppStore = "launch-app-store";
    public const string LiveSixMonths = "live-six-months";
    public const string LiveOneYear = "live-one-year";
    public const string FundTeammate = "fund-teammate";
}
