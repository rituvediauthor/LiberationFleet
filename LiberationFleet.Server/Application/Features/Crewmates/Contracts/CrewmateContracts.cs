namespace LiberationFleet.Server.Application.Features.Crewmates.Contracts;

public class CrewmateGiftStatsDto
{
    public int SacrificeCountLastSeason { get; set; }
    public decimal AverageMonthlyContributions { get; set; }
    public decimal LifetimeContributions { get; set; }
    public decimal ReceptionThisYear { get; set; }
}

public class CrewmatePlatformDisplayDto
{
    public string PlatformName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool IsSharedWithViewer { get; set; }
}

public class CrewmatePaymentPlatformDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
}

public enum CrewmateFriendshipStateDto
{
    None = 0,
    RequestSent = 1,
    RequestReceived = 2,
    Friends = 3,
    Blocked = 4
}

public class CrewmateListItemDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime? LastLoginAt { get; set; }
    public bool IsSelf { get; set; }
    public CrewmatePlatformDisplayDto? PlatformDisplay { get; set; }
    public CrewmateFriendshipStateDto FriendshipState { get; set; }
}

public class CrewmateListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<CrewmateListItemDto> Items { get; set; } = Array.Empty<CrewmateListItemDto>();
}

public class CrewmateProfileDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<CrewmatePaymentPlatformDto> PaymentPlatforms { get; set; } = Array.Empty<CrewmatePaymentPlatformDto>();
    public int SacrificeCountLastSeason { get; set; }
    public decimal AverageMonthlyContributions { get; set; }
    public bool MembershipStatus { get; set; }
    public decimal LifetimeContributions { get; set; }
    public decimal ReceptionThisYear { get; set; }
    public int PriorityScore { get; set; }
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public bool IsSurvivalThresholdRecipient { get; set; }
    public CrewmateFriendshipStateDto FriendshipState { get; set; }
    public bool IsSelf { get; set; }
}

public class CrewmateProfileResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CrewmateProfileDto? Profile { get; set; }
}

public class KickedCrewmateListItemDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class KickedCrewmateListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<KickedCrewmateListItemDto> Items { get; set; } = Array.Empty<KickedCrewmateListItemDto>();
}

public class CrewmateOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CrewmateFriendshipStateDto FriendshipState { get; set; }
}

public class CrewmateKickResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProposalId { get; set; }
}
