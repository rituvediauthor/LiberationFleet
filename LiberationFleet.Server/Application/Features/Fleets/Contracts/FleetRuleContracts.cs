namespace LiberationFleet.Server.Application.Features.Fleets.Contracts;

public class FleetRuleDto
{
    public int Id { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime UpdatedAt { get; set; }
    public bool IsPublic { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}

public class FleetRuleListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<FleetRuleDto> Items { get; set; } = Array.Empty<FleetRuleDto>();
}

public class FleetRuleDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public FleetRuleDto? Rule { get; set; }
}

public class FleetRuleOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RuleId { get; set; }
    public bool ProposalsSubmitted { get; set; }
    public int? ProposalId { get; set; }
}

public class WriteFleetRuleRequest
{
    public bool IsPublic { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
}
