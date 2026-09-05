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

namespace LiberationFleet.Server.Application.Features.Crypto.Commands.UpsertEncryptedContent;

public record UpsertEncryptedContentCommand(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int? CrewId,
    int? FleetId,
    int KeyVersion,
    string Nonce,
    string Ciphertext,
    int? RecipientUserId = null) : IRequest<CryptoOperationResponse>;

public class UpsertEncryptedContentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    IFriendshipRepository friendshipRepository,
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
    private const int MaxMediaCiphertextChars = 40 * 1024 * 1024;
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

        // Profile avatars are always personal (user content key) so leaving/deleting a crew
        // cannot wipe the picture with empty-crew encrypted cleanup.
        var effectiveCrewId = domainType == EncryptedContentType.ProfileAvatar ? null : request.CrewId;
        var effectiveFleetId = domainType == EncryptedContentType.ProfileAvatar ? null : request.FleetId;

        var hasCrewScope = effectiveCrewId.HasValue;
        var hasFleetScope = effectiveFleetId.HasValue;
        var isPersonalAvatar = domainType == EncryptedContentType.ProfileAvatar;
        var isPersonalMedia = AttachmentTypes.Contains(domainType) && !hasCrewScope && !hasFleetScope;
        if (!isPersonalAvatar && !isPersonalMedia && hasCrewScope == hasFleetScope)
        {
            return new CryptoOperationResponse { Success = false, Message = "Exactly one of crew or fleet scope is required." };
        }

        var userId = currentUser.UserId.Value;
        int? recipientUserId = null;

        if (isPersonalMedia)
        {
            if (!request.RecipientUserId.HasValue || request.RecipientUserId.Value <= 0)
            {
                return new CryptoOperationResponse
                {
                    Success = false,
                    Message = "Recipient is required for personal media."
                };
            }

            if (request.RecipientUserId.Value == userId)
            {
                return new CryptoOperationResponse
                {
                    Success = false,
                    Message = "Recipient cannot be yourself."
                };
            }

            var friendship = await friendshipRepository.GetBetweenUsersAsync(
                userId,
                request.RecipientUserId.Value,
                cancellationToken);
            if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
            {
                return new CryptoOperationResponse
                {
                    Success = false,
                    Message = "You can only share personal media with accepted friends."
                };
            }

            recipientUserId = request.RecipientUserId.Value;
        }

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
            if (!await membershipRepository.IsUserInCrewAsync(userId, effectiveCrewId!.Value, cancellationToken))
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not in this crew." };
            }
        }
        else if (hasFleetScope && !await fleetRepository.IsUserInFleetAsync(userId, effectiveFleetId!.Value, cancellationToken))
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
                if (existing.CrewId != effectiveCrewId!.Value)
                {
                    return new CryptoOperationResponse { Success = false, Message = "Encrypted content not found in this crew." };
                }
            }
            else if (hasFleetScope && existing.FleetId != effectiveFleetId!.Value)
            {
                return new CryptoOperationResponse { Success = false, Message = "Encrypted content not found in this fleet." };
            }
            else if (isPersonalAvatar && existing.AuthorUserId != userId)
            {
                return new CryptoOperationResponse { Success = false, Message = "Encrypted content not found." };
            }
            else if (isPersonalMedia
                && (existing.CrewId.HasValue || existing.FleetId.HasValue || existing.AuthorUserId != userId))
            {
                return new CryptoOperationResponse { Success = false, Message = "Encrypted content not found." };
            }

            if (existing.AuthorUserId != userId)
            {
                return new CryptoOperationResponse { Success = false, Message = "Only the author can update this encrypted content." };
            }

            await deepFreezeService.DeleteColdBlobIfPresentAsync(existing, cancellationToken);
        }

        if (hasCrewScope && AttachmentTypes.Contains(domainType))
        {
            var membership = await membershipRepository.GetMembershipAsync(userId, effectiveCrewId!.Value, cancellationToken);
            var crew = await crewRepository.GetByIdAsync(effectiveCrewId.Value, cancellationToken);
            if (membership is null || crew is null)
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not allowed to attach files in this crew." };
            }

            var giftStats = new CrewmateGiftStatsDto();
            try
            {
                giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
                    userId,
                    effectiveCrewId.Value,
                    crew.CurrentSeasonStartDate,
                    cancellationToken);
            }
            catch
            {
                // Schema drift / transient gift-stats failures should not block media uploads.
            }
            var crewTenureDays = await contentTenureService.GetCrewTenureDaysAsync(
                userId,
                effectiveCrewId.Value,
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
            var fleet = await fleetRepository.GetByIdAsync(effectiveFleetId!.Value, cancellationToken);
            if (membership is null || fleet is null
                || !await fleetRepository.IsUserInFleetAsync(userId, effectiveFleetId.Value, cancellationToken))
            {
                return new CryptoOperationResponse { Success = false, Message = "You are not allowed to attach files in this fleet." };
            }

            var giftStats = new CrewmateGiftStatsDto();
            try
            {
                giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
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
                effectiveFleetId.Value,
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

        var envelope = new EncryptedContentEnvelope
        {
            ContentType = domainType,
            ResourceId = request.ResourceId.Trim(),
            CrewId = effectiveCrewId,
            FleetId = effectiveFleetId,
            AuthorUserId = userId,
            RecipientUserId = recipientUserId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CiphertextCharLength = request.Ciphertext.Trim().Length,
            StorageTier = EncryptedContentStorageTier.Hot,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        };

        // Video/audio ciphertext is too large for reliable SQL LOB + gateway round-trips.
        if (domainType == EncryptedContentType.VideoAsset || domainType == EncryptedContentType.AudioAsset)
        {
            await deepFreezeService.OffloadEnvelopeAsync(envelope, cancellationToken);
        }

        await cryptoRepository.UpsertEnvelopeAsync(envelope, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return new CryptoOperationResponse { Success = true, Message = "Encrypted content saved." };
    }
}

