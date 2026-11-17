using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand() : IRequest<NotificationOperationResponse>;

public class MarkAllNotificationsReadCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    ActionRequiredNotificationLiveness actionRequiredLiveness,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkAllNotificationsReadCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var staleActionRequired = await notificationRepository.GetUnreadRequiresActionForUserAsync(
            userId,
            cancellationToken);
        foreach (var notification in staleActionRequired)
        {
            if (!await actionRequiredLiveness.IsActionStillRequiredAsync(notification, cancellationToken))
            {
                notification.RequiresAction = false;
                notification.IsRead = true;
            }
        }

        await notificationRepository.MarkAllReadAsync(userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var summary = await notificationService.PushBadgeSummaryAndGetAsync(userId, cancellationToken);
        return new NotificationOperationResponse
        {
            Success = true,
            Message = "All notifications marked as read.",
            UnreadCount = summary.UnreadCount
        };
    }
}
