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
    public int SacrificeCountLastSeason { get; set; }
    public decimal AverageMonthlyContributions { get; set; }
    public bool MembershipStatus { get; set; }
    public decimal LifetimeContributions { get; set; }
    public decimal ReceptionThisYear { get; set; }
    public int PercentBoost { get; set; }
    public int PriorityScore { get; set; }
}

public class UserProfileDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public IReadOnlyList<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = Array.Empty<PaymentPlatformAccountDto>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public bool NeedsSurvivalAid { get; set; }
    public bool IsSurvivalThresholdRecipient { get; set; }
    public UserProfileStatsDto Stats { get; set; } = new();
}

public class ProfileOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public UserProfileDto? Profile { get; set; }
}
