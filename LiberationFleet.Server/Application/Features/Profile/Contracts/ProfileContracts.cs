namespace LiberationFleet.Server.Application.Features.Profile.Contracts;

public class PaymentPlatformAccountDto
{
    public int Id { get; set; }
    public int PlatformId { get; set; }
    public string? CustomPlatformName { get; set; }
    public string Platform { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
}

public class UserProfileStatsDto
{
    /// <summary>
    /// Emergency sacrifices from the previous season; drives <see cref="PercentBoost"/> this season.
    /// </summary>
    public int SacrificeCountLastSeason { get; set; }

    /// <summary>Emergency sacrifices recorded during the active season (not yet converted to percent boost).</summary>
    public int SacrificeCountThisSeason { get; set; }

    public decimal AverageMonthlyContributions { get; set; }
    public bool MembershipStatus { get; set; }
    public decimal LifetimeContributions { get; set; }
    public decimal ReceptionThisYear { get; set; }
    public int PercentBoost { get; set; }
    public int PriorityScore { get; set; }
    public decimal DonationsPreviousTaxYearUsd { get; set; }
    public decimal DonationsCurrentTaxYearUsd { get; set; }
    public int CurrentTaxYear { get; set; }
    public int PreviousTaxYear { get; set; }
}

public class PriorityScoreBreakdownDto
{
    public int Score { get; set; }
    public decimal CrewLifetimeContributions { get; set; }
    public int EmergencyLevel { get; set; }
    public decimal MembershipBonus { get; set; }
    public decimal UserLifetimeContributions { get; set; }
    public decimal SurvivalThresholdAmount { get; set; }
    public decimal BaseScore { get; set; }
    public int PeopleRepresentedCount { get; set; }
    public int DisabilityLevel { get; set; }
    public int PriorityMultiplier { get; set; }
    public int PercentBoost { get; set; }
    public decimal SacrificeBonusFactor { get; set; }
    public bool IsFinancialMember { get; set; }
    /// <summary>
    /// Optional reception-status note for Giving Season (e.g. not in need).
    /// </summary>
    public string? StatusReason { get; set; }
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string? AvatarResourceId { get; set; }
    public IReadOnlyList<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = Array.Empty<PaymentPlatformAccountDto>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public IReadOnlyList<string> IdentityGroups { get; set; } = Array.Empty<string>();
    public bool NeedsSurvivalAid { get; set; }
    public bool IsSurvivalThresholdRecipient { get; set; }
    public bool CanToggleInNeedOff { get; set; }
    public decimal InNeedToggleThreshold { get; set; }
    public UserProfileStatsDto Stats { get; set; } = new();
    public PriorityScoreBreakdownDto? GivingSeasonPriority { get; set; }
    public PriorityScoreBreakdownDto? LibraryOfThingsPriority { get; set; }
}

public class ProfileOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserProfileDto? Profile { get; set; }
}
