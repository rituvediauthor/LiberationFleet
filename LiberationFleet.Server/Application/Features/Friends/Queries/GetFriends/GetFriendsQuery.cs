using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.GetFriends;

public record GetFriendsQuery(string? Search) : IRequest<FriendListResponse>;

public class GetFriendsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserRepository userRepository,
    IDirectMessageRepository directMessageRepository,
    INotificationRepository notificationRepository) : IRequestHandler<GetFriendsQuery, FriendListResponse>
{
    public async Task<FriendListResponse> Handle(GetFriendsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FriendListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new FriendListResponse { Success = false, Message = "You are not in a crew." };
        }

        var friendships = await friendshipRepository.GetForUserAsync(userId, cancellationToken);
        var acceptedFriendships = friendships
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .ToList();

        var friendIds = acceptedFriendships
            .Select(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .Distinct()
            .ToList();

        if (friendIds.Count == 0)
        {
            return new FriendListResponse
            {
                Success = true,
                Message = "Friends loaded.",
                Items = Array.Empty<FriendListItemDto>()
            };
        }

        var lastMessageAtByFriendId = await directMessageRepository.GetLastMessageAtByFriendUserIdAsync(
            userId,
            friendIds,
            cancellationToken);
        var mutedContents = await notificationRepository.GetMutedContentsAsync(userId, cancellationToken);
        var mutedFriendIds = mutedContents
            .Where(m => m.ContentType == MutedContentType.Friend)
            .Select(m => m.ResourceId)
            .ToHashSet();

        var search = request.Search?.Trim();
        var items = new List<FriendListItemDto>();
        foreach (var friendId in friendIds)
        {
            var friend = await userRepository.GetByIdWithProfileAsync(friendId, cancellationToken);
            if (friend is null)
            {
                continue;
            }

            if (!string.IsNullOrWhiteSpace(search)
                && !friend.Username.Contains(search, StringComparison.OrdinalIgnoreCase))
            {
                continue;
            }

            lastMessageAtByFriendId.TryGetValue(friendId, out var lastMessageAt);
            items.Add(new FriendListItemDto
            {
                UserId = friend.Id,
                Username = friend.Username,
                AvatarResourceId = friend.AvatarResourceId,
                LastLoginAt = friend.LastLoginAt,
                LastMessageAt = lastMessageAt,
                IsMuted = mutedFriendIds.Contains(friendId)
            });
        }

        items = items
            .OrderByDescending(i => i.LastMessageAt ?? DateTime.MinValue)
            .ThenBy(i => i.Username, StringComparer.OrdinalIgnoreCase)
            .ToList();

        return new FriendListResponse
        {
            Success = true,
            Message = "Friends loaded.",
            Items = items
        };
    }
}
