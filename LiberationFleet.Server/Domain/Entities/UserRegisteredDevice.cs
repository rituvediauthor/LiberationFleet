namespace LiberationFleet.Server.Domain.Entities;

public class UserRegisteredDevice
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public string DeviceId { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public string UserAgent { get; set; } = string.Empty;
    public DateTime FirstSeenAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsTrusted { get; set; }
    public bool IsBlocked { get; set; }

    public User User { get; set; } = null!;
}
