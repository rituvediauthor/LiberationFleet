using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.GetFriendRequests;

public record GetFriendRequestsQuery : IRequest<FriendRequestListResponse>;

public class GetFriendRequestsQueryHandler(
    ICurrentUserService currentUser,
    IFriendshipRepository friendshipRepository,
    IUserRepository userRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetFriendRequestsQuery, FriendRequestListResponse>
{
    public async Task<FriendRequestListResponse> Handle(GetFriendRequestsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FriendRequestListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var friendships = await friendshipRepository.GetForUserAsync(userId, cancellationToken);
        var pending = friendships
            .Where(f => f.Status == FriendshipStatus.Pending)
            .OrderByDescending(f => f.CreatedAt)
            .ToList();

        var items = new List<FriendRequestListItemDto>();
        foreach (var friendship in pending)
        {
            var otherUserId = friendship.RequesterUserId == userId
                ? friendship.AddresseeUserId
                : friendship.RequesterUserId;

            if (hiddenUserIds.Contains(otherUserId))
            {
                continue;
            }

            var otherUser = await userRepository.GetByIdWithProfileAsync(otherUserId, cancellationToken);
            if (otherUser is null)
            {
                continue;
            }

            items.Add(new FriendRequestListItemDto
            {
                UserId = otherUser.Id,
                Username = otherUser.Username,
                AvatarResourceId = otherUser.AvatarResourceId,
                LastLoginAt = otherUser.LastLoginAt,
                Direction = friendship.RequesterUserId == userId
                    ? FriendRequestDirectionDto.Outgoing
                    : FriendRequestDirectionDto.Incoming,
                CreatedAt = friendship.CreatedAt
            });
        }

        return new FriendRequestListResponse
        {
            Success = true,
            Message = "Friend requests loaded.",
            Items = items
        };
    }
}
