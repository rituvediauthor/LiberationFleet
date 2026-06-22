using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Opaque encrypted payload. The server stores and routes ciphertext without decrypting it.
/// </summary>
public class EncryptedContentEnvelope
{
    public int Id { get; set; }
    public EncryptedContentType ContentType { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public int? CrewId { get; set; }
    public int AuthorUserId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Crew? Crew { get; set; }
    public User AuthorUser { get; set; } = null!;
}
