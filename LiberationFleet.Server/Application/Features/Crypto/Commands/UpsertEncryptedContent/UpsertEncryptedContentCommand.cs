using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContent;

public record UpsertEncryptedContentCommand(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int? CrewId,
    int? FleetId,
    int KeyVersion,
    string Nonce,
    string Ciphertext) : IRequest<CryptoOperationResponse>;

public class UpsertEncryptedContentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    IMediaDeepFreezeService deepFreezeService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpsertEncryptedContentCommand, CryptoOperationResponse>
{
    private static readonly HashSet<EncryptedContentType> ClientUpsertAllowedTypes =
    [
        EncryptedContentType.GiftLogEntry,
        EncryptedContentType.ImageAsset,
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset,
        EncryptedContentType.ProfileAvatar
    ];

    private static readonly HashSet<EncryptedContentType> AttachmentTypes =
    [
        EncryptedContentType.ImageAsset,
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset
    ];

    /// <summary>
    /// Caps media ciphertext length (characters). Opaque E2E payloads cannot be MIME-sniffed;
    /// size limits still blunt storage / request abuse.
    /// </summary>
    private const int MaxMediaCiphertextChars = 20 * 1024 * 1024;
    private const int MaxGiftLogCiphertextChars = 512 * 1024;
    private const int MaxProfileAvatarCiphertextChars = 5 * 1024 * 1024;

    public async Task<CryptoOperationResponse> Handle(UpsertEncryptedContentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CryptoOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.ResourceId)
            || string.IsNullOrWhiteSpace(request.Nonce)
            || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted content payload is required." };
        }

        var ciphertextLength = request.Ciphertext.Trim().Length;
        var domainType = CryptoMapper.ToDomain(request.ContentType);

        if (AttachmentTypes.Contains(domainType) && ciphertextLength > MaxMediaCiphertextChars)
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted attachment is too large." };
        }

        if (domainType == EncryptedContentType.GiftLogEntry && ciphertextLength > MaxGiftLogCiphertextChars)
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted gift log entry is too large." };
        }

        if (domainType == EncryptedContentType.ProfileAvatar && ciphertextLength > MaxProfileAvatarCiphertextChars)
        {
            return new CryptoOperationResponse { Success = false, Message = "Encrypted profile picture is too large." };
        }

        var hasCrewScope = request.CrewId.HasValue;
        var hasFleetScope = request.FleetId.HasValue;
        if (hasCrewScope == hasFleetScope)
        {
            return new CryptoOperationResponse { Success = false, Message = "Exactly one of crew or fleet scope is required." };
        }

        if (domainType == EncryptedContentType.ProfileAvatar && !hasCrewScope)
        {
            return new CryptoOperationResponse
            {
                Success = false,
                Message = "Profile pictures require crew membership. Join a crew to upload an avatar."
            };
        }

        var userId = currentUser.UserId.Value;

        if (!ClientUpsertAllowedTypes.Contains(domainType))
        {
            return new CryptoOperationResponse
            {
                Success = false,
                Message = "This content type must be saved through its feature API."
            };
        }

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

        if (hasCrewScope && AttachmentTypes.Contains(domainType))
        {
            var membership = await membershipRepository.GetMembershipAsync(userId, request.CrewId!.Value, cancellationToken);
            var crew = await crewRepository.GetByIdAsync(request.CrewId.Value, cancellationToken);
            if (membership is null || crew is null)
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not allowed to attach files in this crew." };
            }

            var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                userId,
                request.CrewId.Value,
                crew.CurrentSeasonStartDate,
                cancellationToken);
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

        if (hasFleetScope && AttachmentTypes.Contains(domainType))
        {
            var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
            var fleet = await fleetRepository.GetByIdAsync(request.FleetId!.Value, cancellationToken);
            if (membership is null || fleet is null
                || !await fleetRepository.IsUserInFleetAsync(userId, request.FleetId.Value, cancellationToken))
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not allowed to attach files in this fleet." };
            }

            var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                userId,
                membership.CrewId,
                membership.Crew?.CurrentSeasonStartDate,
                cancellationToken);
            var fleetTenureDays = await contentTenureService.GetFleetTenureDaysAsync(
                userId,
                request.FleetId.Value,
                cancellationToken);

            if (!FleetContentPermissionService.CanAttachFilesToFleetContent(
                    fleet,
                    membership,
                    giftStats.LifetimeContributions,
                    fleetTenureDays))
            {
                return new CryptoOperationResponse
                {
                    Success = false,
                    Message = "You are not allowed to attach files in this fleet."
                };
            }
        }

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = domainType,
            ResourceId = request.ResourceId.Trim(),
            CrewId = request.CrewId,
            FleetId = request.FleetId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CiphertextCharLength = request.Ciphertext.Trim().Length,
            StorageTier = EncryptedContentStorageTier.Hot,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Encrypted content saved." };
    }
}

