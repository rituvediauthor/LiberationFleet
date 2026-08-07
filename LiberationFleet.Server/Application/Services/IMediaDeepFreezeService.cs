using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public interface IMediaDeepFreezeService
{
    /// <summary>Move eligible hot media ciphertext to cold storage. Returns envelopes frozen.</summary>
    Task<int> FreezeBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>
    /// Immediately upload ciphertext to blob and clear it on the in-memory envelope.
    /// No-ops when blob store is disabled or the payload is too small.
    /// </summary>
    Task OffloadEnvelopeAsync(EncryptedContentEnvelope envelope, CancellationToken cancellationToken = default);

    /// <summary>
    /// Upload raw AES-GCM bytes to cold storage (`.cipher.bin`). Requires blob store enabled.
    /// </summary>
    Task OffloadEnvelopeBytesAsync(
        EncryptedContentEnvelope envelope,
        byte[] ciphertextBytes,
        CancellationToken cancellationToken = default);

    /// <summary>Fill Ciphertext on deep-frozen envelopes from cold storage (in-memory only).</summary>
    Task HydrateAsync(IReadOnlyList<EncryptedContentEnvelope> envelopes, CancellationToken cancellationToken = default);

    /// <summary>
    /// Load raw ciphertext bytes for playback/download (supports legacy base64 blobs and `.cipher.bin`).
    /// </summary>
    Task<byte[]?> LoadCiphertextBytesAsync(
        EncryptedContentEnvelope envelope,
        CancellationToken cancellationToken = default);

    /// <summary>Delete cold blob if present (call before/with SQL delete).</summary>
    Task DeleteColdBlobIfPresentAsync(EncryptedContentEnvelope envelope, CancellationToken cancellationToken = default);
}
