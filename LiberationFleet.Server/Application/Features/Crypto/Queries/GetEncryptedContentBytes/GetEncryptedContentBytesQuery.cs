using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContentBytes;

/// <summary>
/// Single-resource media ciphertext as raw AES-GCM bytes (avoids multi‑MB JSON base64 on the wire).
/// </summary>
public record EncryptedContentBytesResult(
    string ResourceId,
    int KeyVersion,
    string Nonce,
    byte[] CiphertextBytes);

public record GetEncryptedContentBytesQuery(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int? CrewId = null,
    int? FleetId = null) : IRequest<EncryptedContentBytesResult?>;

public class GetEncryptedContentBytesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    ICryptoRepository cryptoRepository,
    IMediaDeepFreezeService deepFreezeService) : IRequestHandler<GetEncryptedContentBytesQuery, EncryptedContentBytesResult?>
{
    public async Task<EncryptedContentBytesResult?> Handle(
        GetEncryptedContentBytesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue || string.IsNullOrWhiteSpace(request.ResourceId))
        {
            return null;
        }

        var hasCrewScope = request.CrewId.HasValue;
        var hasFleetScope = request.FleetId.HasValue;
        var isPersonalMedia = PersonalEncryptedMediaAccess.IsAttachmentType(request.ContentType)
            && PersonalEncryptedMediaAccess.IsPersonalScope(request.CrewId, request.FleetId);
        if (!isPersonalMedia && hasCrewScope == hasFleetScope)
        {
            return null;
        }

        var userId = currentUser.UserId.Value;
        if (hasCrewScope)
        {
            if (!await membershipRepository.IsUserInCrewAsync(userId, request.CrewId!.Value, cancellationToken))
            {
                return null;
            }
        }
        else if (hasFleetScope && !await fleetRepository.IsUserInFleetAsync(userId, request.FleetId!.Value, cancellationToken))
        {
            return null;
        }

        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            CryptoMapper.ToDomain(request.ContentType),
            new[] { request.ResourceId.Trim() },
            crewId: request.CrewId,
            fleetId: request.FleetId,
            personalScopeOnly: isPersonalMedia,
            cancellationToken: cancellationToken);

        if (envelopes.Count == 0)
        {
            return null;
        }

        var envelope = envelopes[0];
        if (isPersonalMedia
            && !await PersonalEncryptedMediaAccess.CanAccessAsync(
                userId,
                envelope,
                friendshipRepository,
                blockRepository,
                cancellationToken))
        {
            return null;
        }

        if (string.IsNullOrWhiteSpace(envelope.Nonce))
        {
            return null;
        }

        var ciphertextBytes = await deepFreezeService.LoadCiphertextBytesAsync(envelope, cancellationToken);
        if (ciphertextBytes is null || ciphertextBytes.Length == 0)
        {
            return null;
        }

        return new EncryptedContentBytesResult(
            envelope.ResourceId,
            envelope.KeyVersion,
            envelope.Nonce,
            ciphertextBytes);
    }
}
