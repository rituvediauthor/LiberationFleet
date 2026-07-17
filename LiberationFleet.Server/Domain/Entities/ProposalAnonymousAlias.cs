namespace LiberationFleet.Server.Domain.Entities;

public class ProposalAnonymousAlias
{
    public const int MaxRerolls = 2;

    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int UserId { get; set; }
    public string Nickname { get; set; } = string.Empty;
    public int RerollCount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Proposal Proposal { get; set; } = null!;
    public User User { get; set; } = null!;

    public int RerollsRemaining => Math.Max(0, MaxRerolls - RerollCount);
}
