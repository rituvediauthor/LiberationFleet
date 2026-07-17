namespace LiberationFleet.Server.Application.Features.Fleets.Contracts;

public class FleetDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int CrewCount { get; set; }
    public string Privacy { get; set; } = string.Empty;
    public string Scope { get; set; } = string.Empty;
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public double? DistanceMiles { get; set; }
    public bool RequireApprovalForEdits { get; set; } = true;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }
    public string? ImageResourceId { get; set; }
}

public class UpdateFleetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Privacy { get; set; } = "Public";
    public string Scope { get; set; } = "Online";
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public bool RequireApprovalForEdits { get; set; } = true;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }
    public string? ImageResourceId { get; set; }
}

public class CreateFleetRequest
{
    public string Name { get; set; } = string.Empty;
    public string Privacy { get; set; } = "Public";
    public string Scope { get; set; } = "Online";
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
}

public class FleetMembershipStatusDto
{
    public bool HasFleet { get; set; }
    public int? FleetId { get; set; }
    public string? FleetName { get; set; }
    public bool AllowCrossCrewGiving { get; set; }
    public string? JoinCode { get; set; }
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public bool NeedsRuleAcceptance { get; set; }
    public string? ImageResourceId { get; set; }
}

public class InviteCrewToFleetRequest
{
    public string JoinCode { get; set; } = string.Empty;
}

public class InviteCrewToFleetResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProposalId { get; set; }
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
}

public class CrewLookupDto
{
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public bool AlreadyInFleet { get; set; }
    public bool IsOwnCrew { get; set; }
}

public class CrewLookupResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public CrewLookupDto? Crew { get; set; }
}

public class AcceptFleetRulesBody
{
    public IReadOnlyList<int> AcceptedRuleIds { get; set; } = Array.Empty<int>();
}

public class FleetOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public FleetDto? Fleet { get; set; }
    public bool ProposalsSubmitted { get; set; }
    public int ProposalsCreated { get; set; }
}

public class FleetSearchResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<FleetDto> Items { get; set; } = Array.Empty<FleetDto>();
    public int Page { get; set; }
    public int PageSize { get; set; }
    public int TotalCount { get; set; }
    public int TotalPages { get; set; }
}

public class PublicFleetRuleDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class PublicFleetRulesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int FleetId { get; set; }
    public string FleetName { get; set; } = string.Empty;
    public IReadOnlyList<PublicFleetRuleDto> Items { get; set; } = Array.Empty<PublicFleetRuleDto>();
}

public class SubmitFleetJoinRequestBody
{
    public int? FleetId { get; set; }
    public string? JoinCode { get; set; }
    public IReadOnlyList<int> AcceptedRuleIds { get; set; } = Array.Empty<int>();
}

public class FleetJoinRequestListItemDto
{
    public int ProposalId { get; set; }
    public int FleetId { get; set; }
    public string FleetName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ApproveCount { get; set; }
    public int DisapproveCount { get; set; }
    public DateTime? ApprovalTimerEndsAt { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FleetJoinRequestListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<FleetJoinRequestListItemDto> Items { get; set; } = Array.Empty<FleetJoinRequestListItemDto>();
}

public class FleetJoinRequestOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProposalId { get; set; }
}

public class FleetCrewListItemDto
{
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class FleetCrewListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<FleetCrewListItemDto> Items { get; set; } = Array.Empty<FleetCrewListItemDto>();
}

public class FleetCrewmateDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
}

public class FleetCrewDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public FleetCrewDetailDto? Crew { get; set; }
}

public class FleetCrewDetailDto
{
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public int MemberCount { get; set; }
    public int? MaxSize { get; set; }
    public bool IsOwnCrew { get; set; }
    public bool CanKick { get; set; }
    public bool CanJoin { get; set; }
    public IReadOnlyList<FleetCrewmateDto> Crewmates { get; set; } = Array.Empty<FleetCrewmateDto>();
}

public class ProposeFleetKickCrewBody
{
    public string? Reason { get; set; }
}

public class FleetLibraryStatusDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool LibraryOfThingsEnabled { get; set; }
    public int? FleetId { get; set; }
}
