namespace LiberationFleet.Server.Domain.Entities;

public class User
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string PasswordHash { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? LastLoginAt { get; set; }
    public bool IsActive { get; set; } = true;
    public bool InNeedOfAid { get; set; } = true;
    public int EmergencyLevel { get; set; }
    public bool NeedsSurvivalAid { get; set; }

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<CrewMembership> CrewMemberships { get; set; } = new List<CrewMembership>();
    public ICollection<UserPaymentPlatform> PaymentPlatforms { get; set; } = new List<UserPaymentPlatform>();
}
