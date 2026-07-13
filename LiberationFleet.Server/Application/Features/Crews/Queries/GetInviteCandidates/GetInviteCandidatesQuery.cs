using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetInviteCandidates;

public record GetInviteCandidatesQuery(string? Username, bool FriendsOnly) : IRequest<InviteCandidateListResponse>;

public class GetInviteCandidatesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository) : IRequestHandler<GetInviteCandidatesQuery, InviteCandidateListResponse>
{
    public async Task<InviteCandidateListResponse> Handle(
        GetInviteCandidatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new InviteCandidateListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        if (await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken) is null)
        {
            return new InviteCandidateListResponse { Success = false, Message = "You are not in a crew." };
        }

        var friendships = await friendshipRepository.GetForUserAsync(userId, cancellationToken);
        var friendIds = friendships
            .Where(f => f.Status == FriendshipStatus.Accepted)
            .Select(f => f.RequesterUserId == userId ? f.AddresseeUserId : f.RequesterUserId)
            .ToHashSet();

        var candidates = new List<InviteCandidateDto>();

        if (request.FriendsOnly || string.IsNullOrWhiteSpace(request.Username))
        {
            foreach (var friendId in friendIds)
            {
                if (await membershipRepository.GetActiveMembershipAsync(friendId, cancellationToken) is not null)
                {
                    continue;
                }

                var friend = await userRepository.GetByIdWithProfileAsync(friendId, cancellationToken);
                if (friend is null || friend.IsUnclaimedPlaceholder)
                {
                    continue;
                }

                candidates.Add(new InviteCandidateDto
                {
                    UserId = friend.Id,
                    Username = friend.Username,
                    IsFriend = true
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(request.Username) && !request.FriendsOnly)
        {
            var users = await userRepository.SearchByUsernameAsync(request.Username, 20, cancellationToken);
            foreach (var user in users)
            {
                if (user.Id == userId)
                {
                    continue;
                }

                if (await membershipRepository.GetActiveMembershipAsync(user.Id, cancellationToken) is not null)
                {
                    continue;
                }

                if (candidates.Any(c => c.UserId == user.Id))
                {
                    continue;
                }

                candidates.Add(new InviteCandidateDto
                {
                    UserId = user.Id,
                    Username = user.Username,
                    IsFriend = friendIds.Contains(user.Id)
                });
            }
        }

        return new InviteCandidateListResponse
        {
            Success = true,
            Message = "Candidates loaded.",
            Items = candidates.OrderBy(c => c.Username).ToList()
        };
    }
}
