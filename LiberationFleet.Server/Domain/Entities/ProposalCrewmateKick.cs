namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewmateKick
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int TargetUserId { get; set; }
    public int SourceProposalId { get; set; }
    public int? SourceCommentId { get; set; }
    public string AnonymousNickname { get; set; } = string.Empty;
    public string? RevealedUsername { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
