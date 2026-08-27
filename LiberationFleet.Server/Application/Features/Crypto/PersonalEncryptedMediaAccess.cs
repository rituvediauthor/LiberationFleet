using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crypto;

/// <summary>
/// Personal (null crew/fleet) media used for friend DMs — author or intended recipient only.
/// </summary>
public static class PersonalEncryptedMediaAccess
{
    private static readonly HashSet<EncryptedContentType> AttachmentTypes =
    [
        EncryptedContentType.ImageAsset,
        EncryptedContentType.VideoAsset,
        EncryptedContentType.AudioAsset
    ];

    public static bool IsAttachmentType(EncryptedContentType contentType) =>
        AttachmentTypes.Contains(contentType);

    public static bool IsAttachmentType(EncryptedContentTypeDto contentType) =>
        IsAttachmentType(CryptoMapper.ToDomain(contentType));

    public static bool IsPersonalScope(int? crewId, int? fleetId) =>
        !crewId.HasValue && !fleetId.HasValue;

    public static async Task<bool> CanAccessAsync(
        int viewerUserId,
        EncryptedContentEnvelope envelope,
        IFriendshipRepository friendshipRepository,
        IUserBlockRepository blockRepository,
        CancellationToken cancellationToken)
    {
        if (envelope.CrewId.HasValue || envelope.FleetId.HasValue)
        {
            return false;
        }

        if (envelope.AuthorUserId == viewerUserId)
        {
            return true;
        }

        // Legacy personal media without a recipient is author-only.
        if (!envelope.RecipientUserId.HasValue || envelope.RecipientUserId.Value != viewerUserId)
        {
            return false;
        }

        if (await blockRepository.IsBlockedAsync(viewerUserId, envelope.AuthorUserId, cancellationToken)
            || await blockRepository.IsBlockedAsync(envelope.AuthorUserId, viewerUserId, cancellationToken))
        {
            return false;
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(
            viewerUserId,
            envelope.AuthorUserId,
            cancellationToken);
        return friendship is not null && friendship.Status == FriendshipStatus.Accepted;
    }

    public static async Task<IReadOnlyList<EncryptedContentEnvelope>> FilterAccessibleAsync(
        int viewerUserId,
        IReadOnlyList<EncryptedContentEnvelope> envelopes,
        IFriendshipRepository friendshipRepository,
        IUserBlockRepository blockRepository,
        CancellationToken cancellationToken)
    {
        if (envelopes.Count == 0)
        {
            return envelopes;
        }

        var allowed = new List<EncryptedContentEnvelope>(envelopes.Count);
        foreach (var envelope in envelopes)
        {
            if (await CanAccessAsync(
                    viewerUserId,
                    envelope,
                    friendshipRepository,
                    blockRepository,
                    cancellationToken))
            {
                allowed.Add(envelope);
            }
        }

        return allowed;
    }
}
