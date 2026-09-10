namespace LiberationFleet.Server.Domain.Enums;

public enum CrewSettingField
{
    Name = 0,
    MaxSize = 1,
    Privacy = 2,
    Scope = 3,
    /// <summary>Comma-separated allowlist of postal codes (Local crews). Replaces legacy single ZipCode.</summary>
    AllowedZipCodes = 4,
    /// <summary>Legacy radius field; no longer written. Kept for historical proposal rows.</summary>
    RadiusMiles = 5,
    AllowSurvivalThresholds = 6,
    RequireApprovalForEdits = 7,
    InNeedDefaultThreshold = 8,
    LibraryOfThingsEnabled = 9,
    MemberCycleCapMode = 10,
    MemberCycleCapFixedAmount = 11,
    MemberCycleCapMultiplier = 12,
    NonMemberCycleCapMode = 13,
    NonMemberCycleCapFixedAmount = 14,
    NonMemberCycleCapMultiplier = 15,
    AllowCrewmateFileAttachments = 16,
    MinimumCrewmateTenureDaysForAttachments = 17,
    MinimumContributionForAttachments = 18,
    MinimumCrewmateTenureDaysForProposals = 19,
    MinimumContributionForProposals = 20,
    AllowCrossCrewGiving = 21,
    ImageResourceId = 22,
    FinancialMembershipContributionFloor = 23,
    DuoVoteTimeoutMode = 24,
    AutoResolveOverTime = 25,
    BaseAutoResolveHours = 26,
    ChangeAutoResolveTimerOnFirstReject = 27,
    AutoResolveHoursAfterFirstReject = 28,
    /// <summary>ISO 3166-1 alpha-2 country for Local postal matching.</summary>
    CountryCode = 29
}
