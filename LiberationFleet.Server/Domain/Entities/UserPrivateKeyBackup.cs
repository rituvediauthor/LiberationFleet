namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Password-wrapped private identity key backup. The server stores ciphertext only.
/// </summary>
public class UserPrivateKeyBackup
{
    public int UserId { get; set; }
    public string Salt { get; set; } = string.Empty;
    public string Iv { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
