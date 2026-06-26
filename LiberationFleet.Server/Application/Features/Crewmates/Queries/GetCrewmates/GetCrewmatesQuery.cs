using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Queries.GetCrewmates;

public record GetCrewmatesQuery : IRequest<CrewmateListResponse>;

public class GetCrewmatesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetCrewmatesQuery, CrewmateListResponse>
{
    public async Task<CrewmateListResponse> Handle(GetCrewmatesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateListResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (membership is null)
        {
            return new CrewmateListResponse { Success = false, Message = "You are not in a crew." };
        }

        var viewer = await userRepository.GetByIdWithProfileAsync(viewerId, cancellationToken);
        if (viewer is null)
        {
            return new CrewmateListResponse { Success = false, Message = "User not found." };
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(membership.CrewId, cancellationToken);
        var friendships = await friendshipRepository.GetForUserAsync(viewerId, cancellationToken);
        var friendshipByUserId = friendships.ToDictionary(
            f => f.RequesterUserId == viewerId ? f.AddresseeUserId : f.RequesterUserId,
            f => f);

        var items = new List<CrewmateListItemDto>();
        foreach (var member in members.OrderBy(m => m.User.Username))
        {
            friendshipByUserId.TryGetValue(member.UserId, out var friendship);
            var viewerBlockedTarget = await blockRepository.IsBlockedAsync(viewerId, member.UserId, cancellationToken);
            var targetBlockedViewer = await blockRepository.IsBlockedAsync(member.UserId, viewerId, cancellationToken);

            items.Add(new CrewmateListItemDto
            {
                UserId = member.UserId,
                Username = member.User.Username,
                LastLoginAt = member.User.LastLoginAt,
                IsSelf = member.UserId == viewerId,
                PlatformDisplay = CrewmateMapper.MapPlatformDisplay(viewer, member.User),
                FriendshipState = CrewmateMapper.MapFriendshipState(
                    viewerId,
                    member.UserId,
                    friendship,
                    viewerBlockedTarget,
                    targetBlockedViewer)
            });
        }

        return new CrewmateListResponse
        {
            Success = true,
            Message = "Crewmates loaded.",
            Items = items
        };
    }
}
