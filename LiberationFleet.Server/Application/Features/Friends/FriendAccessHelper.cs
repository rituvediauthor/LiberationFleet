using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Friends;

public static class DirectMessageMapper
{
    public static DirectMessageDto MapMessage(DirectMessage message, EncryptedContentEnvelope? envelope) => new()
    {
        Id = message.Id,
        AuthorUserId = message.AuthorUserId,
        AuthorUsername = envelope is null ? message.AuthorUser?.Username ?? string.Empty : string.Empty,
        AuthorAvatarResourceId = message.AuthorUser?.AvatarResourceId,
        CreatedAt = message.CreatedAt,
        HasEncryptedContent = envelope is not null,
        EncryptedPayload = envelope is null ? null : CryptoMapper.MapPayload(envelope)
    };
}

public static class FriendAccessHelper
{
    public static async Task<FriendAccessResult> ValidateFriendMessagingAsync(
        ICurrentUserService currentUser,
        ICrewMembershipRepository membershipRepository,
        IFriendshipRepository friendshipRepository,
        IUserBlockRepository blockRepository,
        IUserRepository userRepository,
        int friendUserId,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return FriendAccessResult.Fail("Unauthorized.");
        }

        var viewerId = currentUser.UserId.Value;
        if (viewerId == friendUserId)
        {
            return FriendAccessResult.Fail("You cannot message yourself.");
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (membership is null)
        {
            return FriendAccessResult.Fail("You are not in a crew.");
        }

        if (!await membershipRepository.IsUserInCrewAsync(friendUserId, membership.CrewId, cancellationToken))
        {
            return FriendAccessResult.Fail("Friend not found.");
        }

        if (await blockRepository.IsBlockedAsync(viewerId, friendUserId, cancellationToken)
            || await blockRepository.IsBlockedAsync(friendUserId, viewerId, cancellationToken))
        {
            return FriendAccessResult.Fail("You cannot message this user.");
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(viewerId, friendUserId, cancellationToken);
        if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
        {
            return FriendAccessResult.Fail("You can only message friends.");
        }

        var friend = await userRepository.GetByIdWithProfileAsync(friendUserId, cancellationToken);
        if (friend is null)
        {
            return FriendAccessResult.Fail("Friend not found.");
        }

        return FriendAccessResult.Ok(viewerId, membership.CrewId, friend.Username);
    }
}

public sealed class FriendAccessResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ViewerId { get; init; }
    public int CrewId { get; init; }
    public string FriendUsername { get; init; } = string.Empty;

    public static FriendAccessResult Ok(int viewerId, int crewId, string friendUsername) =>
        new()
        {
            Success = true,
            ViewerId = viewerId,
            CrewId = crewId,
            FriendUsername = friendUsername
        };

    public static FriendAccessResult Fail(string message) =>
        new() { Success = false, Message = message };
}
