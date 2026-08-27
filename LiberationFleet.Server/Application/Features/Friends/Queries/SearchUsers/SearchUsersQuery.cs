using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Queries.SearchUsers;

public record SearchUsersQuery(string Username) : IRequest<UserSearchResponse>;

public class SearchUsersQueryHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
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
        var users = await userRepository.SearchByUsernameAsync(query, MaxResults, cancellationToken);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var friendships = await friendshipRepository.GetForUserAsync(userId, cancellationToken);
        var friendshipByUserId = friendships
            .GroupBy(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .ToDictionary(g => g.Key, g => g.OrderBy(f => f.CreatedAt).First());

        var items = new List<UserSearchResultDto>();
        foreach (var user in users)
        {
            if (user.Id == userId || hiddenUserIds.Contains(user.Id))
            {
                continue;
            }

            friendshipByUserId.TryGetValue(user.Id, out var friendship);
            items.Add(new UserSearchResultDto
            {
                UserId = user.Id,
                Username = user.Username,
                FriendshipState = CrewmateMapper.MapFriendshipState(
                    userId,
                    user.Id,
                    friendship,
                    false,
                    false)
            });

            if (items.Count >= MaxResults)
            {
                break;
            }
        }

        return new UserSearchResponse
        {
            Success = true,
            Message = "Search complete.",
            Items = items
        };
    }
}
