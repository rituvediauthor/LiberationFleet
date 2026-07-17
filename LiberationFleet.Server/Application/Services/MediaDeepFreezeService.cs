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

    public async Task<int> FreezeBatchAsync(CancellationToken cancellationToken = default)
    {
        var opts = options.Value;
        if (!opts.Enabled || !blobStore.IsEnabled)
        {
            return 0;
        }

        var cutoff = DateTime.UtcNow.AddDays(-Math.Max(1, opts.AgeDays));
        var candidates = await cryptoRepository.GetDeepFreezeCandidatesAsync(
            FreezableTypes,
            cutoff,
            Math.Clamp(opts.BatchSize, 1, 500),
            Math.Max(0, opts.MinimumCiphertextChars),
            cancellationToken);

        var frozen = 0;
        foreach (var envelope in candidates)
        {
            if (string.IsNullOrEmpty(envelope.Ciphertext))
            {
                continue;
            }

            var path = BuildBlobPath(envelope);
            try
            {
                await blobStore.UploadAsync(path, envelope.Ciphertext, cancellationToken);
                envelope.CiphertextCharLength = envelope.Ciphertext.Length;
                envelope.Ciphertext = string.Empty;
                envelope.ColdBlobPath = path;
                envelope.StorageTier = EncryptedContentStorageTier.DeepFreeze;
                envelope.FrozenAt = DateTime.UtcNow;
                envelope.UpdatedAt = DateTime.UtcNow;
                frozen++;
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
            logger.LogInformation("Deep-froze {Count} media envelopes older than {Days} days.", frozen, opts.AgeDays);
        }

        return frozen;
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
