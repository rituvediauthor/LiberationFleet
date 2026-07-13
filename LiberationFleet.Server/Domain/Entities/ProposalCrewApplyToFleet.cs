namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewApplyToFleet
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int FleetId { get; set; }
    public string? TargetJoinCode { get; set; }
    public string AcceptedRuleIdsJson { get; set; } = "[]";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public bool InitiatedByFleetInvite { get; set; }

    public Proposal Proposal { get; set; } = null!;
    public Fleet Fleet { get; set; } = null!;
}
