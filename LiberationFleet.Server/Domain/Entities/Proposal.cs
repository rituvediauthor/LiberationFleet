using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class Proposal
{
    public int Id { get; set; }
    public int? CrewId { get; set; }
    public int? FleetId { get; set; }
    public int AuthorUserId { get; set; }
    public ProposalKind Kind { get; set; } = ProposalKind.General;
    public ProposalStatus Status { get; set; } = ProposalStatus.Pending;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public DateTime? ApprovalTimerEndsAt { get; set; }
    public int ApproveCount { get; set; }
    public int DisapproveCount { get; set; }
    public bool IsDeleted { get; set; }

    public Crew? Crew { get; set; }
    public Fleet? Fleet { get; set; }
    public User AuthorUser { get; set; } = null!;
    public ICollection<ProposalVote> Votes { get; set; } = new List<ProposalVote>();
    public ICollection<ProposalComment> Comments { get; set; } = new List<ProposalComment>();
    public ProposalCrewSettingChange? CrewSettingChange { get; set; }
    public ProposalCrewRuleChange? CrewRuleChange { get; set; }
    public ProposalCrewChatChange? CrewChatChange { get; set; }
    public ProposalCrewmateKick? CrewmateKick { get; set; }
    public ProposalCrewmateRejoin? CrewmateRejoin { get; set; }
    public ProposalCrewJoinRequest? CrewJoinRequest { get; set; }
    public ProposalCrewRoleChange? CrewRoleChange { get; set; }
    public ProposalClaimPlaceholderIdentity? ClaimPlaceholderIdentity { get; set; }
    public ProposalCrewmatePermissionGrant? CrewmatePermissionGrant { get; set; }
    public ProposalCrewApplyToFleet? CrewApplyToFleet { get; set; }
    public ProposalFleetJoinRequest? FleetJoinRequest { get; set; }
    public ProposalFleetSettingChange? FleetSettingChange { get; set; }
    public ProposalFleetKickCrew? FleetKickCrew { get; set; }
    public ProposalFleetRuleChange? FleetRuleChange { get; set; }
    public ProposalFleetNotice? FleetNotice { get; set; }
}
