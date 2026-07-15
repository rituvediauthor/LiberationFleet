namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Fleet symmetric content key wrapped for a specific member. Enables fleet-wide E2EE for chat, forums, etc.
/// </summary>
public class FleetKeyDistribution
{
    public int Id { get; set; }
    public int FleetId { get; set; }
    public int UserId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string WrappedFleetKey { get; set; } = string.Empty;
    public string WrapNonce { get; set; } = string.Empty;
    public int WrappedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Fleet Fleet { get; set; } = null!;
    public User User { get; set; } = null!;
    public User WrappedByUser { get; set; } = null!;
}
