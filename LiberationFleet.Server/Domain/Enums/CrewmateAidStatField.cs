namespace LiberationFleet.Server.Domain.Enums;

public enum CrewmateAidStatField
{
    EstimatedMonthlyContribution = 1,
    LifetimeContributions = 2,
    ReceptionThisYear = 3,
    TotalReceptionAmount = 4,
    SurvivalThresholdReceived = 5,
    CycleReceived = 6,
    CycleCompleted = 7,
    /// <summary>Active percent boost applied to priority score this season (membership PercentBonus).</summary>
    PercentBoost = 8,
    /// <summary>
    /// JSON payload (<see cref="Application.Features.Crews.AidSeasonAccountingDto"/>) for cycle lock/order,
    /// survival thresholds, auto-join, and pre-season drafts.
    /// </summary>
    SeasonAccounting = 9,
    InNeedOfAid = 10,
    NeedsSurvivalAid = 11,
    EmergencyLevel = 12,
    DisabilityLevel = 13,
    PeopleRepresentedCount = 14,
    /// <summary>
    /// JSON array of payment platforms
    /// (<c>platformId</c>, <c>platformName</c>, <c>handle</c>, <c>isPreferred</c>, optional <c>customPlatformName</c>).
    /// </summary>
    PaymentPlatforms = 15
}
