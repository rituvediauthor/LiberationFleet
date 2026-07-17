using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Opaque encrypted payload. The server stores and routes ciphertext without decrypting it.
/// Media assets older than the deep-freeze age may have ciphertext moved to cold blob storage.
/// </summary>
public class EncryptedContentEnvelope
{
    public int Id { get; set; }
    public EncryptedContentType ContentType { get; set; }
    public string ResourceId { get; set; } = string.Empty;
    public int? CrewId { get; set; }
    public int? FleetId { get; set; }
    public int AuthorUserId { get; set; }
    public int KeyVersion { get; set; } = 1;
    public string Nonce { get; set; } = string.Empty;
    /// <summary>Empty when <see cref="StorageTier"/> is DeepFreeze (bytes live in cold storage).</summary>
    public string Ciphertext { get; set; } = string.Empty;
    public EncryptedContentStorageTier StorageTier { get; set; } = EncryptedContentStorageTier.Hot;
    /// <summary>Blob path within the deep-freeze container when StorageTier is DeepFreeze.</summary>
    public string? ColdBlobPath { get; set; }
    public DateTime? FrozenAt { get; set; }
    /// <summary>UTF-16 char length of ciphertext before freeze (approximate SQL space freed).</summary>
    public int CiphertextCharLength { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Crew? Crew { get; set; }
    public Fleet? Fleet { get; set; }
    public User AuthorUser { get; set; } = null!;
}
