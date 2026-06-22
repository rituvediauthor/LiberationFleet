namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Crew symmetric content key wrapped for a specific member. Enables crew-wide E2EE for logs, chat, etc.
/// </summary>
public class CrewKeyDistribution
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int UserId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string WrappedCrewKey { get; set; } = string.Empty;
    public string WrapNonce { get; set; } = string.Empty;
    public int WrappedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Crew Crew { get; set; } = null!;
    public User User { get; set; } = null!;
    public User WrappedByUser { get; set; } = null!;
}
