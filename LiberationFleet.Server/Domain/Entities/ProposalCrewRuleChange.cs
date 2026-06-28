using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewRuleChange
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public CrewRuleProposalAction Action { get; set; }
    public int? RuleId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Nonce { get; set; }
    public string? Ciphertext { get; set; }
    public int KeyVersion { get; set; } = 1;
    public bool IsApplied { get; set; }
    public bool IsPublic { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
