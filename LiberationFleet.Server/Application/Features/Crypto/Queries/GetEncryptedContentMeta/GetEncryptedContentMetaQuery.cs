using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crypto.Queries.GetEncryptedContentMeta;

/// <summary>
/// Envelope metadata only (nonce / key version) — never loads ciphertext bytes.
/// Used by the client to choose plain streaming vs encrypted full download.
/// </summary>
public record EncryptedContentMetaDto(
    string ResourceId,
    int KeyVersion,
    string Nonce);

public record GetEncryptedContentMetaQuery(
    EncryptedContentTypeDto ContentType,
    string ResourceId,
    int? CrewId = null,
    int? FleetId = null) : IRequest<EncryptedContentMetaDto?>;

public class GetEncryptedContentMetaQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetEncryptedContentMetaQuery, EncryptedContentMetaDto?>
{
    public async Task<EncryptedContentMetaDto?> Handle(
        GetEncryptedContentMetaQuery request,
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

        if (envelopes.Count == 0 || string.IsNullOrWhiteSpace(envelopes[0].Nonce))
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

        return new EncryptedContentMetaDto(
            envelope.ResourceId,
            envelope.KeyVersion,
            envelope.Nonce);
    }
}
