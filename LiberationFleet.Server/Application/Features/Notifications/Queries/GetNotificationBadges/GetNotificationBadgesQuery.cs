using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Queries.GetNotificationBadges;

public record GetNotificationBadgesQuery : IRequest<NotificationBadgeSummaryResponse>;

public class GetNotificationBadgesQueryHandler(
    ICurrentUserService currentUser,
    NotificationBadgeSummaryService badgeSummaryService) : IRequestHandler<GetNotificationBadgesQuery, NotificationBadgeSummaryResponse>
{
    public async Task<NotificationBadgeSummaryResponse> Handle(GetNotificationBadgesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationBadgeSummaryResponse { Success = false, Message = "Unauthorized." };
        }

        return await badgeSummaryService.GetForUserAsync(currentUser.UserId.Value, cancellationToken);
    }
}
