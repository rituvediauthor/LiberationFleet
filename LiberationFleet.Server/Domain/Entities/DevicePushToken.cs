using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// FCM / APNs device token for background push. Distinct from <see cref="UserRegisteredDevice"/> (login trust).
/// </summary>
public class DevicePushToken
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public PushPlatform Platform { get; set; }
    public string Token { get; set; } = string.Empty;
    /// <summary>Optional Capacitor / OS device id for replacing tokens on the same device.</summary>
    public string? DeviceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;
    public bool IsDisabled { get; set; }

    public User User { get; set; } = null!;
}
