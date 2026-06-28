namespace LiberationFleet.Server.Domain.Entities;

public class ProposalAnonymousAlias
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Proposal Proposal { get; set; } = null!;
    public User User { get; set; } = null!;
}
