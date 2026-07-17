using LiberationFleet.Server.Domain.Enums;

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
    public bool IsUnclaimedPlaceholder { get; set; }
    public bool IsCrewGiftRecipient { get; set; }
    public bool InNeedOfAid { get; set; } = true;
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public bool NeedsSurvivalAid { get; set; }
    public int PercentBonus { get; set; }
    public AdultContentPreference AdultContentPreference { get; set; } = AdultContentPreference.Block;
    public bool TwoFactorEnabled { get; set; }
    public bool LockSettingsWithPassword { get; set; }
    public string? SettingsLockPasswordHash { get; set; }
    public int FailedLoginAttempts { get; set; }
    public DateTime? LastFailedLoginAt { get; set; }
    public DateTime? LastDonationCampaignPromptAt { get; set; }
    public string? AvatarResourceId { get; set; }

    public ICollection<PasswordResetToken> PasswordResetTokens { get; set; } = new List<PasswordResetToken>();
    public ICollection<CrewMembership> CrewMemberships { get; set; } = new List<CrewMembership>();
    public ICollection<UserPaymentPlatform> PaymentPlatforms { get; set; } = new List<UserPaymentPlatform>();
    public ICollection<UserRegisteredDevice> RegisteredDevices { get; set; } = new List<UserRegisteredDevice>();
    public ICollection<SecurityAlert> SecurityAlerts { get; set; } = new List<SecurityAlert>();
    public ICollection<AppDonation> AppDonations { get; set; } = new List<AppDonation>();
}
