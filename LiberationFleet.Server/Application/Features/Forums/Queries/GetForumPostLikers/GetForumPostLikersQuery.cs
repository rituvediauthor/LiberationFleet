using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Engagement.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetForumPostLikers;

public record GetForumPostLikersQuery(int PostId) : IRequest<ContentLikersResponse>;

public class GetForumPostLikersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetForumPostLikersQuery, ContentLikersResponse>
{
    public async Task<ContentLikersResponse> Handle(GetForumPostLikersQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ContentLikersResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ContentLikersResponse { Success = false, Message = "Forum post not found." };
        }

        int crewId;
        if (post.CrewId.HasValue)
        {
            if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId.Value, cancellationToken))
            {
                return new ContentLikersResponse { Success = false, Message = "You are not in this crew." };
            }

            crewId = post.CrewId.Value;
        }
        else if (post.FleetId.HasValue)
        {
            if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
            {
                return new ContentLikersResponse { Success = false, Message = "You are not in this fleet." };
            }

            var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
            if (membership is null)
            {
                return new ContentLikersResponse { Success = false, Message = "You are not in a crew." };
            }

            crewId = membership.CrewId;
        }
        else
        {
            return new ContentLikersResponse { Success = false, Message = "Forum post not found." };
        }

        var likers = await forumRepository.GetActivePostLikersAsync(post.Id, cancellationToken);
        var avatarAllowed = await crewAvatarVisibility.GetUsersAllowedToShowCrewAvatarAsync(crewId, cancellationToken);

        return new ContentLikersResponse
        {
            Success = true,
            Message = "Likers loaded.",
            Items = likers.Select(l => new ContentLikerDto
            {
                UserId = l.UserId,
                Username = l.Username,
                AvatarResourceId = CrewAvatarVisibilityService.Filter(l.AvatarResourceId, l.UserId, avatarAllowed)
            }).ToList()
        };
    }
}
