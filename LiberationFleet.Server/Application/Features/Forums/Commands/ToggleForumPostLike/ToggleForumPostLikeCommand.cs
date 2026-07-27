using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Commands.ToggleForumPostLike;

public record ToggleForumPostLikeCommand(int PostId) : IRequest<ForumLikeToggleResponse>;

public class ToggleForumPostLikeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleForumPostLikeCommand, ForumLikeToggleResponse>
{
    public async Task<ForumLikeToggleResponse> Handle(
        ToggleForumPostLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumLikeToggleResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdWithAuthorAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ForumLikeToggleResponse { Success = false, Message = "Forum post not found." };
        }

        int? crewId = null;
        string actionUrl;

        if (post.CrewId.HasValue)
        {
            if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId.Value, cancellationToken))
            {
                return new ForumLikeToggleResponse { Success = false, Message = "You are not in this crew." };
            }

            crewId = post.CrewId.Value;
            actionUrl = $"/app/crew/forums/{post.Id}";
        }
        else if (post.FleetId.HasValue)
        {
            if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
            {
                return new ForumLikeToggleResponse { Success = false, Message = "You are not in this fleet." };
            }

            var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
            crewId = membership?.CrewId;
            actionUrl = $"/app/fleet/forums/{post.Id}";
        }
        else
        {
            return new ForumLikeToggleResponse { Success = false, Message = "Forum post not found." };
        }

        var existing = await forumRepository.GetPostLikeAsync(userId, post.Id, cancellationToken);
        bool liked;
        var utcNow = DateTime.UtcNow;

        if (existing is null)
        {
            var like = new ForumLike
            {
                UserId = userId,
                ForumPostId = post.Id,
                CreatedAt = utcNow
            };

            if (post.AuthorUserId != userId && !like.AuthorNotified)
            {
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = post.AuthorUserId,
                    CrewId = crewId,
                    Kind = NotificationKind.ForumPostLiked,
                    Title = "Forum post liked",
                    Body = "Someone liked your forum post.",
                    ActionUrl = actionUrl,
                    RelatedEntityId = post.Id,
                    ActorUserId = userId
                }, cancellationToken);
                like.AuthorNotified = true;
            }

            await forumRepository.AddLikeAsync(like, cancellationToken);
            liked = true;
        }
        else if (existing.RemovedAt is null)
        {
            existing.RemovedAt = utcNow;
            liked = false;
        }
        else
        {
            existing.RemovedAt = null;
            liked = true;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var likeCounts = await forumRepository.GetActiveLikeCountsForPostsAsync([post.Id], cancellationToken);
        likeCounts.TryGetValue(post.Id, out var likeCount);

        return new ForumLikeToggleResponse
        {
            Success = true,
            Message = liked ? "Post liked." : "Post unliked.",
            Liked = liked,
            LikeCount = likeCount
        };
    }
}
