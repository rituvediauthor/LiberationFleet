using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ProposalFleetRuleChange
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public FleetRuleProposalAction Action { get; set; }
    public int? RuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string RuleTitle { get; set; } = string.Empty;
    public string RuleDescription { get; set; } = string.Empty;
    public bool IsPublic { get; set; }
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
