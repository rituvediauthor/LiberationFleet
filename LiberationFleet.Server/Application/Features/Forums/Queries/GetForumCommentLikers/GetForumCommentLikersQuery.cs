using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Engagement.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Queries.GetForumCommentLikers;

public record GetForumCommentLikersQuery(int PostId, int CommentId) : IRequest<ContentLikersResponse>;

public class GetForumCommentLikersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetForumCommentLikersQuery, ContentLikersResponse>
{
    public async Task<ContentLikersResponse> Handle(
        GetForumCommentLikersQuery request,
        CancellationToken cancellationToken)
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

        var comment = await forumRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ForumPostId != post.Id)
        {
            return new ContentLikersResponse { Success = false, Message = "Comment not found." };
        }

        var likers = await forumRepository.GetActiveCommentLikersAsync(comment.Id, cancellationToken);
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
