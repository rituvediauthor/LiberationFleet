using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.ToggleGiftCommentLike;

public record ToggleGiftCommentLikeCommand(int GiftId, int CommentId) : IRequest<GiftLikeToggleResponse>;

public class ToggleGiftCommentLikeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleGiftCommentLikeCommand, GiftLikeToggleResponse>
{
    public async Task<GiftLikeToggleResponse> Handle(
        ToggleGiftCommentLikeCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftLikeToggleResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftLikeToggleResponse { Success = false, Message = "You are not in a crew." };
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (gift is null || gift.CrewId != membership.CrewId)
        {
            return new GiftLikeToggleResponse { Success = false, Message = "Gift not found." };
        }

        var comment = await giftRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.GiftId != gift.Id)
        {
            return new GiftLikeToggleResponse { Success = false, Message = "Comment not found." };
        }

        var actionUrl = $"/app/crew/gift-log/{gift.Id}?commentId={comment.Id}";
        var existing = await giftRepository.GetGiftCommentLikeAsync(userId, comment.Id, cancellationToken);
        bool liked;
        var utcNow = DateTime.UtcNow;

        if (existing is null)
        {
            var like = new GiftLike
            {
                UserId = userId,
                GiftCommentId = comment.Id,
                CreatedAt = utcNow
            };

            if (comment.AuthorUserId != userId && !like.AuthorNotified)
            {
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = comment.AuthorUserId,
                    CrewId = membership.CrewId,
                    Kind = NotificationKind.GiftCommentLiked,
                    Title = "Gift comment liked",
                    Body = "Someone liked your gift log comment.",
                    ActionUrl = actionUrl,
                    RelatedEntityId = gift.Id,
                    SecondaryEntityId = comment.Id,
                    ActorUserId = userId
                }, cancellationToken);
                like.AuthorNotified = true;
            }

            await giftRepository.AddLikeAsync(like, cancellationToken);
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

        var likeCounts = await giftRepository.GetActiveLikeCountsForGiftCommentsAsync([comment.Id], cancellationToken);
        likeCounts.TryGetValue(comment.Id, out var likeCount);

        return new GiftLikeToggleResponse
        {
            Success = true,
            Message = liked ? "Comment liked." : "Comment unliked.",
            Liked = liked,
            LikeCount = likeCount
        };
    }
}
