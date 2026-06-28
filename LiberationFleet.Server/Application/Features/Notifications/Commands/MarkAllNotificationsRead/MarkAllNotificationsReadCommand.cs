using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.MarkAllNotificationsRead;

public record MarkAllNotificationsReadCommand() : IRequest<NotificationOperationResponse>;

public class MarkAllNotificationsReadCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkAllNotificationsReadCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(MarkAllNotificationsReadCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        await notificationRepository.MarkAllReadAsync(userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new NotificationOperationResponse
        {
            Success = true,
            Message = "All notifications marked as read.",
            UnreadCount = 0
        };
    }
}
