using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.SetMutedContent;

public record SetMutedContentCommand(
    MutedContentType ContentType,
    int ResourceId,
    bool Muted,
    MuteDuration? Duration = null)
    : IRequest<NotificationOperationResponse>;

public class SetMutedContentCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IFriendshipRepository friendshipRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<SetMutedContentCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(SetMutedContentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.ResourceId <= 0)
        {
            return new NotificationOperationResponse { Success = false, Message = "Invalid resource." };
        }

        var userId = currentUser.UserId.Value;

        if (request.ContentType == MutedContentType.Friend && request.Muted)
        {
            var friendship = await friendshipRepository.GetBetweenUsersAsync(
                userId,
                request.ResourceId,
                cancellationToken);
            if (friendship is null || friendship.Status != FriendshipStatus.Accepted)
            {
                return new NotificationOperationResponse
                {
                    Success = false,
                    Message = "You can only mute accepted friends."
                };
            }
        }

        if (request.Muted)
        {
            var utcNow = DateTime.UtcNow;
            var mutedUntil = MuteDurationCalculator.Until(request.Duration, utcNow);
            var existing = await notificationRepository.FindTrackedMuteAsync(
                userId,
                request.ContentType,
                request.ResourceId,
                cancellationToken);

            if (existing is null)
            {
                await notificationRepository.AddMutedContentAsync(new UserMutedContent
                {
                    UserId = userId,
                    ContentType = request.ContentType,
                    ResourceId = request.ResourceId,
                    MutedUntil = mutedUntil,
                    CreatedAt = utcNow
                }, cancellationToken);
            }
            else
            {
                existing.MutedUntil = mutedUntil;
                existing.CreatedAt = utcNow;
            }
        }
        else
        {
            await notificationRepository.RemoveMutedContentAsync(
                userId,
                request.ContentType,
                request.ResourceId,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var summary = await notificationService.PushBadgeSummaryAndGetAsync(userId, cancellationToken);
        return new NotificationOperationResponse
        {
            Success = true,
            Message = request.Muted ? "Notifications muted." : "Notifications unmuted.",
            UnreadCount = summary.UnreadCount
        };
    }
}
