using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public interface IMediaDeepFreezeService
{
    /// <summary>Move eligible hot media ciphertext to cold storage. Returns envelopes frozen.</summary>
    Task<int> FreezeBatchAsync(CancellationToken cancellationToken = default);

    /// <summary>Fill Ciphertext on deep-frozen envelopes from cold storage (in-memory only).</summary>
    Task HydrateAsync(IReadOnlyList<EncryptedContentEnvelope> envelopes, CancellationToken cancellationToken = default);

    /// <summary>Delete cold blob if present (call before/with SQL delete).</summary>
    Task DeleteColdBlobIfPresentAsync(EncryptedContentEnvelope envelope, CancellationToken cancellationToken = default);
}
