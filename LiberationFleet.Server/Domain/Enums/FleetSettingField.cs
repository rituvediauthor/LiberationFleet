namespace LiberationFleet.Server.Domain.Enums;

public enum FleetSettingField
{
    Name = 0,
    Privacy = 1,
    Scope = 2,
    /// <summary>Comma-separated allowlist of postal codes (Local fleets). Replaces legacy single ZipCode.</summary>
    AllowedZipCodes = 3,
    /// <summary>Legacy radius field; no longer written. Kept for historical proposal rows.</summary>
    RadiusMiles = 4,
    RequireApprovalForEdits = 5,
    LibraryOfThingsEnabled = 6,
    AllowCrewmateFileAttachments = 7,
    MinimumCrewmateTenureDaysForAttachments = 8,
    MinimumContributionForAttachments = 9,
    MinimumCrewmateTenureDaysForProposals = 10,
    MinimumContributionForProposals = 11,
    ImageResourceId = 12,
    DuoVoteTimeoutMode = 13,
    AutoResolveOverTime = 14,
    BaseAutoResolveHours = 15,
    ChangeAutoResolveTimerOnFirstReject = 16,
    AutoResolveHoursAfterFirstReject = 17,
    /// <summary>ISO 3166-1 alpha-2 country for Local postal matching.</summary>
    CountryCode = 18
}
