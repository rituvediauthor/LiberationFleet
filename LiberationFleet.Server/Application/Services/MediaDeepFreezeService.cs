using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
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

    public async Task OffloadEnvelopeBytesAsync(
        EncryptedContentEnvelope envelope,
        byte[] ciphertextBytes,
        CancellationToken cancellationToken = default)
    {
        if (!blobStore.IsEnabled)
        {
            throw new InvalidOperationException("Media cold storage is not configured.");
        }

        if (ciphertextBytes.Length == 0)
        {
            throw new InvalidOperationException("Ciphertext bytes are required.");
        }

        var path = BuildBinaryBlobPath(envelope);
        await blobStore.UploadBytesAsync(path, ciphertextBytes, cancellationToken);
        envelope.CiphertextCharLength = ciphertextBytes.Length;
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

            // Binary cold blobs cannot be represented as the legacy base64 string field.
            if (IsBinaryBlobPath(envelope.ColdBlobPath))
            {
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

    public async Task<byte[]?> LoadCiphertextBytesAsync(
        EncryptedContentEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (!string.IsNullOrWhiteSpace(envelope.Ciphertext))
        {
            try
            {
                return Convert.FromBase64String(envelope.Ciphertext.Trim());
            }
            catch (FormatException)
            {
                return null;
            }
        }

        if (envelope.StorageTier != EncryptedContentStorageTier.DeepFreeze
            || string.IsNullOrWhiteSpace(envelope.ColdBlobPath)
            || !blobStore.IsEnabled)
        {
            return null;
        }

        if (IsBinaryBlobPath(envelope.ColdBlobPath))
        {
            return await blobStore.DownloadBytesAsync(envelope.ColdBlobPath, cancellationToken);
        }

        var legacy = await blobStore.DownloadAsync(envelope.ColdBlobPath, cancellationToken);
        if (string.IsNullOrWhiteSpace(legacy))
        {
            return null;
        }

        try
        {
            return Convert.FromBase64String(legacy.Trim());
        }
        catch (FormatException)
        {
            return null;
        }
    }

    public async Task<PlainMediaContentStream?> OpenPlainMediaContentAsync(
        EncryptedContentEnvelope envelope,
        CancellationToken cancellationToken = default)
    {
        if (!PlainMediaFraming.IsPlainNonce(envelope.Nonce))
        {
            return null;
        }

        Stream? rawStream = null;
        try
        {
            long rawLength;
            if (!string.IsNullOrWhiteSpace(envelope.Ciphertext))
            {
                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(envelope.Ciphertext.Trim());
                }
                catch (FormatException)
                {
                    return null;
                }

                rawStream = new MemoryStream(bytes, writable: false);
                rawLength = bytes.Length;
            }
            else if (envelope.StorageTier == EncryptedContentStorageTier.DeepFreeze
                && !string.IsNullOrWhiteSpace(envelope.ColdBlobPath)
                && blobStore.IsEnabled
                && IsBinaryBlobPath(envelope.ColdBlobPath))
            {
                var opened = await blobStore.OpenReadAsync(envelope.ColdBlobPath, cancellationToken);
                if (opened is null)
                {
                    return null;
                }

                rawStream = opened.Value.Stream;
                rawLength = opened.Value.Length;
            }
            else if (envelope.StorageTier == EncryptedContentStorageTier.DeepFreeze
                && !string.IsNullOrWhiteSpace(envelope.ColdBlobPath)
                && blobStore.IsEnabled)
            {
                // Legacy base64 cold blob — fall back to full load into a memory stream.
                var legacy = await blobStore.DownloadAsync(envelope.ColdBlobPath, cancellationToken);
                if (string.IsNullOrWhiteSpace(legacy))
                {
                    return null;
                }

                byte[] bytes;
                try
                {
                    bytes = Convert.FromBase64String(legacy.Trim());
                }
                catch (FormatException)
                {
                    return null;
                }

                rawStream = new MemoryStream(bytes, writable: false);
                rawLength = bytes.Length;
            }
            else
            {
                return null;
            }

            if (!PlainMediaFraming.TryGetHeader(rawStream, out var mimeType, out var headerLength))
            {
                await rawStream.DisposeAsync();
                return null;
            }

            var contentLength = rawLength - headerLength;
            if (contentLength < 0)
            {
                await rawStream.DisposeAsync();
                return null;
            }

            var contentStream = new BoundedReadStream(rawStream, headerLength, contentLength);
            rawStream = null; // ownership transferred
            return new PlainMediaContentStream
            {
                ContentStream = contentStream,
                ContentType = mimeType,
                ContentLength = contentLength
            };
        }
        catch
        {
            if (rawStream is not null)
            {
                await rawStream.DisposeAsync();
            }

            throw;
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

    public static string BuildBinaryBlobPath(EncryptedContentEnvelope envelope)
    {
        var scope = envelope.CrewId.HasValue
            ? $"crew-{envelope.CrewId.Value}"
            : envelope.FleetId.HasValue
                ? $"fleet-{envelope.FleetId.Value}"
                : "unscoped";
        return $"{scope}/{(int)envelope.ContentType}/{envelope.ResourceId}.cipher.bin";
    }

    public static bool IsBinaryBlobPath(string? path) =>
        !string.IsNullOrWhiteSpace(path)
        && path.EndsWith(".cipher.bin", StringComparison.OrdinalIgnoreCase);
}
