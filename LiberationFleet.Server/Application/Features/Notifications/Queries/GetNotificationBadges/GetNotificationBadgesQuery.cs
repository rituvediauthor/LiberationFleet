using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Queries.GetNotificationBadges;

public record GetNotificationBadgesQuery : IRequest<NotificationBadgeSummaryResponse>;

public class GetNotificationBadgesQueryHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUserBlockRepository userBlockRepository) : IRequestHandler<GetNotificationBadgesQuery, NotificationBadgeSummaryResponse>
{
    public async Task<NotificationBadgeSummaryResponse> Handle(GetNotificationBadgesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationBadgeSummaryResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var notifications = await notificationRepository.GetUnreadForUserAsync(userId, cancellationToken);
        var preferences = await notificationRepository.GetPreferencesAsync(userId, cancellationToken);
        var mutedContents = await notificationRepository.GetMutedContentsAsync(userId, cancellationToken);
        var hiddenContents = await notificationRepository.GetHiddenContentsAsync(userId, cancellationToken);
        var hiddenUserIds = await userBlockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);

        return NotificationBadgeBuilder.Build(
            notifications,
            preferences,
            mutedContents,
            hiddenContents,
            hiddenUserIds);
    }
}
