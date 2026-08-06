using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Services;

public sealed class MediaDeepFreezeService(
    ICryptoRepository cryptoRepository,
    IDeepFreezeBlobStore blobStore,
    IUnitOfWork unitOfWork,
    IOptions<MediaDeepFreezeOptions> options,
    ILogger<MediaDeepFreezeService> logger) : IMediaDeepFreezeService
{
    public static readonly EncryptedContentType[] FreezableTypes =
    [
        EncryptedContentType.ImageAsset,
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset
    ];

    /// <summary>Video/audio are offloaded immediately (gateway timeouts on multi‑MB SQL LOBs).</summary>
    private static readonly EncryptedContentType[] ImmediateOffloadTypes =
    [
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset
    ];

    public async Task<int> FreezeBatchAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled || !blobStore.IsEnabled)
        {
            return 0;
        }

        var batchSize = Math.Clamp(opts.BatchSize, 1, 500);
        var minChars = Math.Max(0, opts.MinimumCiphertextChars);
        var imageCutoff = DateTime.UtcNow.AddDays(-Math.Max(1, opts.AgeDays));

        // Video/audio: freeze as soon as they exist (AgeDays does not apply).
        var immediate = await cryptoRepository.GetDeepFreezeCandidatesAsync(
            ImmediateOffloadTypes,
            DateTime.UtcNow.AddMinutes(1),
            batchSize,
            minChars,
            cancellationToken);

        var remaining = Math.Max(0, batchSize - immediate.Count);
        IReadOnlyList<EncryptedContentEnvelope> images = Array.Empty<EncryptedContentEnvelope>();
        if (remaining > 0)
        {
            images = await cryptoRepository.GetDeepFreezeCandidatesAsync(
                [EncryptedContentType.ImageAsset],
                imageCutoff,
                remaining,
                minChars,
                cancellationToken);
        }

        var candidates = immediate.Concat(images).ToList();
        var frozen = 0;
        foreach (var envelope in candidates)
        {
            if (string.IsNullOrEmpty(envelope.Ciphertext))
            {
                continue;
            }

            try
            {
                await OffloadEnvelopeAsync(envelope, cancellationToken);
                if (envelope.StorageTier == EncryptedContentStorageTier.DeepFreeze)
                {
                    frozen++;
                }
            }
            catch (Exception ex)
            {
                logger.LogError(
                    ex,
                    "Failed to deep-freeze envelope {ContentType}/{ResourceId}.",
                    envelope.ContentType,
                    envelope.ResourceId);
            }
        }

        if (frozen > 0)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
            logger.LogInformation(
                "Deep-froze {Count} media envelopes (video/audio immediate; images older than {Days} days).",
                frozen,
                opts.AgeDays);
        }

        return frozen;
    }

    public async Task OffloadEnvelopeAsync(
        EncryptedContentEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled || !blobStore.IsEnabled)
        {
            return;
        }

        if (string.IsNullOrEmpty(envelope.Ciphertext))
        {
            return;
        }

        if (envelope.Ciphertext.Length < Math.Max(0, opts.MinimumCiphertextChars))
        {
            return;
        }

        // Images stay hot until aged unless already in a freeze batch; upsert only auto-offloads video/audio.
        var path = BuildBlobPath(envelope);
        await blobStore.UploadAsync(path, envelope.Ciphertext, cancellationToken);
        envelope.CiphertextCharLength = envelope.Ciphertext.Length;
        envelope.Ciphertext = string.Empty;
        envelope.ColdBlobPath = path;
        envelope.StorageTier = EncryptedContentStorageTier.DeepFreeze;
        envelope.FrozenAt = DateTime.UtcNow;
        envelope.UpdatedAt = DateTime.UtcNow;
    }

    public async Task HydrateAsync(
        IReadOnlyList<EncryptedContentEnvelope> envelopes,
        CancellationToken cancellationToken = default)
    {
        if (!blobStore.IsEnabled || envelopes.Count == 0)
        {
            return;
        }

        foreach (var envelope in envelopes)
        {
            if (envelope.StorageTier != EncryptedContentStorageTier.DeepFreeze)
            {
                continue;
            }

            if (!string.IsNullOrEmpty(envelope.Ciphertext))
            {
                continue;
            }

            if (string.IsNullOrWhiteSpace(envelope.ColdBlobPath))
            {
                logger.LogWarning(
                    "Deep-frozen envelope {ContentType}/{ResourceId} has no ColdBlobPath.",
                    envelope.ContentType,
                    envelope.ResourceId);
                continue;
            }

            var ciphertext = await blobStore.DownloadAsync(envelope.ColdBlobPath, cancellationToken);
            if (ciphertext is null)
            {
                logger.LogWarning(
                    "Missing cold blob for {ContentType}/{ResourceId} at {Path}.",
                    envelope.ContentType,
                    envelope.ResourceId,
                    envelope.ColdBlobPath);
                continue;
            }

            // In-memory only — do not write back to SQL (keeps SQL slim).
            envelope.Ciphertext = ciphertext;
        }
    }

    public async Task DeleteColdBlobIfPresentAsync(
        EncryptedContentEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(envelope.ColdBlobPath) || !blobStore.IsEnabled)
        {
            return;
        }

        try
        {
            await blobStore.DeleteAsync(envelope.ColdBlobPath, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogWarning(
                ex,
                "Failed to delete cold blob {Path} for {ContentType}/{ResourceId}.",
                envelope.ColdBlobPath,
                envelope.ContentType,
                envelope.ResourceId);
        }
    }

    public static string BuildBlobPath(EncryptedContentEnvelope envelope)
    {
        var scope = envelope.CrewId.HasValue
            ? $"crew-{envelope.CrewId.Value}"
            : envelope.FleetId.HasValue
                ? $"fleet-{envelope.FleetId.Value}"
                : "unscoped";
        return $"{scope}/{(int)envelope.ContentType}/{envelope.ResourceId}.cipher";
    }
}
