using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContentBytes;

/// <summary>
/// Upsert video/audio AES-GCM ciphertext as raw bytes (no base64). Requires deep-freeze blob storage.
/// </summary>
public record UpsertEncryptedContentBytesCommand(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int? CrewId,
    int? FleetId,
    int KeyVersion,
    string Nonce,
    byte[] CiphertextBytes) : IRequest<CryptoOperationResponse>;

public class UpsertEncryptedContentBytesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    IMediaDeepFreezeService deepFreezeService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertEncryptedContentBytesCommand, CryptoOperationResponse>
{
    private static readonly HashSet<EncryptedContentType> AllowedTypes =
    [
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset,
        EncryptedContentType.ImageAsset
    ];

    /// <summary>
    /// Raw ciphertext/plain budget for binary uploads (~600 MB plaintext + framing overhead).
    /// </summary>
    private const int MaxMediaCiphertextBytes = 640 * 1024 * 1024;

    public async Task<CryptoOperationResponse> Handle(
        UpsertEncryptedContentBytesCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.ResourceId)
            || string.IsNullOrWhiteSpace(request.Nonce)
            || request.CiphertextBytes is null
            || request.CiphertextBytes.Length == 0)
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted content payload is required." };
        }

        if (request.CiphertextBytes.Length > MaxMediaCiphertextBytes)
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted attachment is too large." };
        }

        var domainType = CryptoMapper.ToDomain(request.ContentType);
        if (!AllowedTypes.Contains(domainType))
        {
            return new CryptoOperationResponse
            {
                Success = false,
                Message = "Binary upload is only supported for image, video, and audio."
            };
        }

        var hasCrewScope = request.CrewId.HasValue;
        var hasFleetScope = request.FleetId.HasValue;
        if (hasCrewScope == hasFleetScope)
        {
            return new CryptoOperationResponse { Success = false, Message = "Exactly one of crew or fleet scope is required." };
        }

        var userId = currentUser.UserId.Value;

        if (hasCrewScope)
        {
            if (!await membershipRepository.IsUserInCrewAsync(userId, request.CrewId!.Value, cancellationToken))
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not in this crew." };
            }
        }
        else if (!await fleetRepository.IsUserInFleetAsync(userId, request.FleetId!.Value, cancellationToken))
        {
            return new CryptoOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        var existing = await cryptoRepository.GetEnvelopeAsync(
            domainType,
            request.ResourceId.Trim(),
            cancellationToken);
        if (existing is not null)
        {
            if (hasCrewScope)
            {
                if (existing.CrewId != request.CrewId!.Value)
                {
                    return new CryptoOperationResponse { Success = false, Message = "Encrypted content not found in this crew." };
                }
            }
            else if (existing.FleetId != request.FleetId!.Value)
            {
                return new CryptoOperationResponse { Success = false, Message = "Encrypted content not found in this fleet." };
            }

            if (existing.AuthorUserId != userId)
            {
                return new CryptoOperationResponse { Success = false, Message = "Only the author can update this encrypted content." };
            }

            await deepFreezeService.DeleteColdBlobIfPresentAsync(existing, cancellationToken);
        }

        if (hasCrewScope)
        {
            var membership = await membershipRepository.GetMembershipAsync(userId, request.CrewId!.Value, cancellationToken);
            var crew = await crewRepository.GetByIdAsync(request.CrewId.Value, cancellationToken);
            if (membership is null || crew is null)
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not allowed to attach files in this crew." };
            }

            var giftStats = new CrewmateGiftStatsDto();
            try
            {
                giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                    userId,
                    request.CrewId.Value,
                    crew.CurrentSeasonStartDate,
                    cancellationToken);
            }
            catch
            {
                // Schema drift / transient gift-stats failures should not block media uploads.
            }
            var crewTenureDays = await contentTenureService.GetCrewTenureDaysAsync(
                userId,
                request.CrewId.Value,
                cancellationToken);

            if (!CrewContentPermissionService.CanAttachFilesToCrewContent(
                    crew,
                    membership,
                    giftStats.LifetimeContributions,
                    crewTenureDays))
            {
                return new CryptoOperationResponse
                {
                    Success = false,
                    Message = "You are not allowed to attach files in this crew."
                };
            }
        }

        if (hasFleetScope)
        {
            var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
            var fleet = await fleetRepository.GetByIdAsync(request.FleetId!.Value, cancellationToken);
            if (membership is null || fleet is null
                || !await fleetRepository.IsUserInFleetAsync(userId, request.FleetId.Value, cancellationToken))
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not allowed to attach files in this fleet." };
            }

            var fleetGiftStats = new CrewmateGiftStatsDto();
            try
            {
                fleetGiftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                    userId,
                    membership.CrewId,
                    membership.Crew?.CurrentSeasonStartDate,
                    cancellationToken);
            }
            catch
            {
                // Schema drift / transient gift-stats failures should not block media uploads.
            }
            var fleetTenureDays = await contentTenureService.GetFleetTenureDaysAsync(
                userId,
                request.FleetId.Value,
                cancellationToken);

            if (!FleetContentPermissionService.CanAttachFilesToFleetContent(
                    fleet,
                    membership,
                    fleetGiftStats.LifetimeContributions,
                    fleetTenureDays))
            {
                return new CryptoOperationResponse
                {
                    Success = false,
                    Message = "You are not allowed to attach files in this fleet."
                };
            }
        }

        var envelope = new EncryptedContentEnvelope
        {
            ContentType = domainType,
            ResourceId = request.ResourceId.Trim(),
            CrewId = request.CrewId,
            FleetId = request.FleetId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = string.Empty,
            CiphertextCharLength = request.CiphertextBytes.Length,
            StorageTier = EncryptedContentStorageTier.Hot,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        try
        {
            await deepFreezeService.OffloadEnvelopeBytesAsync(envelope, request.CiphertextBytes, cancellationToken);
        }
        catch (InvalidOperationException ex)
        {
            return new CryptoOperationResponse { Success = false, Message = ex.Message };
        }

        await cryptoRepository.UpsertEnvelopeAsync(envelope, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Encrypted content saved." };
    }
}
