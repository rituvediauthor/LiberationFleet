namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Public identity key material for E2EE key exchange. Private keys never leave the client except as an encrypted backup.
/// </summary>
public class UserKeyBundle
{
    public int UserId { get; set; }
    public string IdentityPublicKey { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
