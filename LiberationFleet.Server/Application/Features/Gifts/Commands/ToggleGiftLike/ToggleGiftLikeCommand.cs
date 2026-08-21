using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.ToggleGiftLike;

public record ToggleGiftLikeCommand(int GiftId) : IRequest<GiftLikeToggleResponse>;

public class ToggleGiftLikeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleGiftLikeCommand, GiftLikeToggleResponse>
{
    public async Task<GiftLikeToggleResponse> Handle(
        ToggleGiftLikeCommand request,
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

        var actionUrl = $"/app/crew/gift-log/{gift.Id}";
        var existing = await giftRepository.GetGiftLikeAsync(userId, gift.Id, cancellationToken);
        bool liked;
        var utcNow = DateTime.UtcNow;

        if (existing is null)
        {
            var like = new GiftLike
            {
                UserId = userId,
                GiftId = gift.Id,
                CreatedAt = utcNow
            };

            if (!like.AuthorNotified)
            {
                var notifyUserIds = new List<int>();
                if (gift.GiverUserId != userId)
                {
                    notifyUserIds.Add(gift.GiverUserId);
                }

                if (gift.RecipientUserId != userId && gift.RecipientUserId != gift.GiverUserId)
                {
                    notifyUserIds.Add(gift.RecipientUserId);
                }

                if (notifyUserIds.Count > 0)
                {
                    await notificationService.NotifyUsersAsync(
                        notifyUserIds.Select(targetUserId => new CreateNotificationRequest
                        {
                            UserId = targetUserId,
                            CrewId = membership.CrewId,
                            Kind = NotificationKind.GiftEntryLiked,
                            Title = "Gift liked",
                            Body = "Someone liked a gift log entry.",
                            ActionUrl = actionUrl,
                            RelatedEntityId = gift.Id,
                            ActorUserId = userId
                        }),
                        cancellationToken);
                    like.AuthorNotified = true;
                }
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

        var likeCounts = await giftRepository.GetActiveLikeCountsForGiftsAsync([gift.Id], cancellationToken);
        likeCounts.TryGetValue(gift.Id, out var likeCount);

        return new GiftLikeToggleResponse
        {
            Success = true,
            Message = liked ? "Gift liked." : "Gift unliked.",
            Liked = liked,
            LikeCount = likeCount
        };
    }
}
