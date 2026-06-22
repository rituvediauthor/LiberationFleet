namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Anonymous to clients; user identity is stored only for deduplication and is never exposed via the API.
/// </summary>
public class ProposalVote
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int UserId { get; set; }
    public bool IsApprove { get; set; }
    public DateTime VotedAt { get; set; } = DateTime.UtcNow;

    public Proposal Proposal { get; set; } = null!;
    public User User { get; set; } = null!;
}
