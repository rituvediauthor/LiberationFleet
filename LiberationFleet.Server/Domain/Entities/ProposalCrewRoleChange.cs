using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewRoleChange
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int TargetUserId { get; set; }
    public CrewRoleProposalAction Action { get; set; }
    public string RolesJson { get; set; } = "[]";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
