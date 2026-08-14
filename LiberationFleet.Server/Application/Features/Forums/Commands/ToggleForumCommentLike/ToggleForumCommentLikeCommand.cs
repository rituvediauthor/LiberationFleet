using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Commands.ToggleForumCommentLike;

public record ToggleForumCommentLikeCommand(int PostId, int CommentId) : IRequest<ForumLikeToggleResponse>;

public class ToggleForumCommentLikeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleForumCommentLikeCommand, ForumLikeToggleResponse>
{
    public async Task<ForumLikeToggleResponse> Handle(
        ToggleForumCommentLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumLikeToggleResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdAsync(request.PostId, cancellationToken);
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
            actionUrl = $"/app/crew/forums/{post.Id}?commentId={request.CommentId}";
        }
        else if (post.FleetId.HasValue)
        {
            if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
            {
                return new ForumLikeToggleResponse { Success = false, Message = "You are not in this fleet." };
            }

            var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
            crewId = membership?.CrewId;
            actionUrl = $"/app/fleet/forums/{post.Id}?commentId={request.CommentId}";
        }
        else
        {
            return new ForumLikeToggleResponse { Success = false, Message = "Forum post not found." };
        }

        var comment = await forumRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ForumPostId != post.Id)
        {
            return new ForumLikeToggleResponse { Success = false, Message = "Comment not found." };
        }

        var existing = await forumRepository.GetCommentLikeAsync(userId, comment.Id, cancellationToken);
        bool liked;
        var utcNow = DateTime.UtcNow;

        if (existing is null)
        {
            var like = new ForumLike
            {
                UserId = userId,
                ForumCommentId = comment.Id,
                CreatedAt = utcNow
            };

            if (comment.AuthorUserId != userId && !like.AuthorNotified)
            {
                var isFleet = post.FleetId.HasValue;
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = comment.AuthorUserId,
                    CrewId = crewId,
                    Kind = isFleet ? NotificationKind.FleetForumCommentLiked : NotificationKind.ForumCommentLiked,
                    Title = isFleet ? "Fleet comment liked" : "Comment liked",
                    Body = isFleet
                        ? "Someone liked your fleet comment."
                        : "Someone liked your comment.",
                    ActionUrl = actionUrl,
                    RelatedEntityId = post.Id,
                    SecondaryEntityId = comment.Id,
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

        var likeCounts = await forumRepository.GetActiveLikeCountsForCommentsAsync([comment.Id], cancellationToken);
        likeCounts.TryGetValue(comment.Id, out var likeCount);

        return new ForumLikeToggleResponse
        {
            Success = true,
            Message = liked ? "Comment liked." : "Comment unliked.",
            Liked = liked,
            LikeCount = likeCount
        };
    }
}
