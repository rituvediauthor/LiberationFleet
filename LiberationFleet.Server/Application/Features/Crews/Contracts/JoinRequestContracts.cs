namespace LiberationFleet.Server.Application.Features.Crews.Contracts;

public class PublicCrewRuleDto
{
    public int Id { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class PublicCrewRulesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public IReadOnlyList<PublicCrewRuleDto> Items { get; set; } = Array.Empty<PublicCrewRuleDto>();
}

public class SubmitJoinRequestBody
{
    public int? CrewId { get; set; }
    public string? JoinCode { get; set; }
    public IReadOnlyList<int> AcceptedRuleIds { get; set; } = Array.Empty<int>();
}

public class JoinRequestListItemDto
{
    public int ProposalId { get; set; }
    public int CrewId { get; set; }
    public string CrewName { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public int ApproveCount { get; set; }
    public int DisapproveCount { get; set; }
    public DateTime? ApprovalTimerEndsAt { get; set; }
    public bool IsKeyPrepared { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class JoinRequestListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<JoinRequestListItemDto> Items { get; set; } = Array.Empty<JoinRequestListItemDto>();
}

public class JoinRequestOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int ProposalId { get; set; }
}
