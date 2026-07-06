namespace LiberationFleet.Server.Domain.Entities;

public class ProposalClaimPlaceholderIdentity
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int PlaceholderUserId { get; set; }
    public int ClaimantUserId { get; set; }
    public string PlaceholderDisplayName { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
