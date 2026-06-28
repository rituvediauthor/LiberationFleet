using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.SearchUsers;

public record SearchUsersQuery(string Username) : IRequest<UserSearchResponse>;

public class SearchUsersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<SearchUsersQuery, UserSearchResponse>
{
    private const int MaxResults = 25;

    public async Task<UserSearchResponse> Handle(SearchUsersQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new UserSearchResponse { Success = false, Message = "Unauthorized." };
        }

        var query = request.Username?.Trim();
        if (string.IsNullOrWhiteSpace(query))
        {
            return new UserSearchResponse
            {
                Success = true,
                Message = "Enter a username to search.",
                Items = Array.Empty<UserSearchResultDto>()
            };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new UserSearchResponse { Success = false, Message = "You are not in a crew." };
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(membership.CrewId, cancellationToken);
        var friendships = await friendshipRepository.GetForUserAsync(userId, cancellationToken);
        var friendshipByUserId = friendships.ToDictionary(
            f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId,
            f => f);

        var items = new List<UserSearchResultDto>();
        foreach (var member in members
                     .Where(m => m.UserId != userId)
                     .Where(m => m.User.Username.Contains(query, StringComparison.OrdinalIgnoreCase))
                     .OrderBy(m => m.User.Username)
                     .Take(MaxResults))
        {
            friendshipByUserId.TryGetValue(member.UserId, out var friendship);
            var viewerBlockedTarget = await blockRepository.IsBlockedAsync(userId, member.UserId, cancellationToken);
            var targetBlockedViewer = await blockRepository.IsBlockedAsync(member.UserId, userId, cancellationToken);

            items.Add(new UserSearchResultDto
            {
                UserId = member.UserId,
                Username = member.User.Username,
                FriendshipState = CrewmateMapper.MapFriendshipState(
                    userId,
                    member.UserId,
                    friendship,
                    viewerBlockedTarget,
                    targetBlockedViewer)
            });
        }

        return new UserSearchResponse
        {
            Success = true,
            Message = "Search complete.",
            Items = items
        };
    }
}
