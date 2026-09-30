using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class EmailMfaChallenge
{
    public int Id { get; set; }
    public int UserId { get; set; }
    /// <summary>Opaque client token returned after password check / begin-enable.</summary>
    public string ChallengeToken { get; set; } = string.Empty;
    /// <summary>SHA-256 hex of the one-time code bound to this challenge.</summary>
    public string CodeHash { get; set; } = string.Empty;
    public EmailMfaPurpose Purpose { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime ExpiresAt { get; set; }
    public DateTime? ConsumedAt { get; set; }
    public DateTime LastSentAt { get; set; } = DateTime.UtcNow;
    public int AttemptCount { get; set; }
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }

    public User User { get; set; } = null!;
}
