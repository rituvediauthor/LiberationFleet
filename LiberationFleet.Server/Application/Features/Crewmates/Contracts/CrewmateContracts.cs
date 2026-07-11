using LiberationFleet.Server.Application.Features.Profile.Contracts;

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
    public bool IsPlaceholderMember { get; set; }
    public CrewmatePlatformDisplayDto? PlatformDisplay { get; set; }
    public CrewmateFriendshipStateDto FriendshipState { get; set; }
}

public class CrewmateListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<CrewmateListItemDto> Items { get; set; } = Array.Empty<CrewmateListItemDto>();
}

public class CrewmateMentionCandidateDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class CrewmateMentionSearchResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<CrewmateMentionCandidateDto> Items { get; set; } = Array.Empty<CrewmateMentionCandidateDto>();
}

public class CrewmateProfileDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
    public IReadOnlyList<CrewmateElectedRoleDto> ElectedRoles { get; set; } = Array.Empty<CrewmateElectedRoleDto>();
    public IReadOnlyList<CrewmatePaymentPlatformDto> PaymentPlatforms { get; set; } = Array.Empty<CrewmatePaymentPlatformDto>();
    public int SacrificeCountLastSeason { get; set; }
    public decimal AverageMonthlyContributions { get; set; }
    public bool MembershipStatus { get; set; }
    public decimal LifetimeContributions { get; set; }
    public decimal ReceptionThisYear { get; set; }
    public int PriorityScore { get; set; }
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; }
    public int DisabilityLevel { get; set; }
    public bool IsSurvivalThresholdRecipient { get; set; }
    public CrewmateFriendshipStateDto FriendshipState { get; set; }
    public bool IsSelf { get; set; }
    public bool CanAttachFiles { get; set; }
    public bool CanCreateProposals { get; set; }
    public bool CanAttachFilesToCrewContent { get; set; }
    public bool CanCreateCrewProposals { get; set; }
    public bool CanProposeAttachFilesGrant { get; set; }
    public bool CanProposeCreateProposalsGrant { get; set; }
    public int CrewmateTenureDays { get; set; }
    public bool CanToggleCanAttachFiles { get; set; }
    public bool CanModerateAttachments { get; set; }
    public bool CanExportCrewData { get; set; }
    public bool IsPlaceholderMember { get; set; }
    public bool CanClaimIdentity { get; set; }
}

public class CrewmateElectedRoleDto
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
}

public class CrewRoleDefinitionDto
{
    public string Role { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class CrewRoleDefinitionsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<CrewRoleDefinitionDto> Roles { get; set; } = Array.Empty<CrewRoleDefinitionDto>();
}

public class CrewRoleChangeRequest
{
    public List<string> Roles { get; set; } = [];
}

public class CrewRoleChangeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProposalId { get; set; }
}

public class ProposeCrewmatePermissionGrantRequest
{
    public string GrantType { get; set; } = string.Empty;
}

public class ToggleCanAttachFilesRequest
{
    public bool CanAttachFiles { get; set; }
}

public class CrewmateStateExportItemDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal LifetimeContributions { get; set; }
    public decimal ReceptionThisYear { get; set; }
    public int PriorityScore { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; }
    public int DisabilityLevel { get; set; }
    public int SacrificeCountLastSeason { get; set; }
    public bool IsSurvivalThresholdRecipient { get; set; }
    public decimal? EstimatedMonthlyContribution { get; set; }
    public IReadOnlyList<CrewmatePaymentPlatformDto> PaymentPlatforms { get; set; } = Array.Empty<CrewmatePaymentPlatformDto>();
    public IReadOnlyList<string> Roles { get; set; } = Array.Empty<string>();
}

public class CrewmateStatesExportResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public DateTime ExportedAt { get; set; }
    public IReadOnlyList<CrewmateStateExportItemDto> Items { get; set; } = Array.Empty<CrewmateStateExportItemDto>();
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

public class KickCrewmateRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class AddPlaceholderCrewmateRequest
{
    public string Name { get; set; } = string.Empty;
    public List<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = [];
}

public class AddPlaceholderCrewmateResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UserId { get; set; }
}
