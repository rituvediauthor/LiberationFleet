using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.MarkNotificationRead;

public record MarkNotificationReadCommand(int NotificationId) : IRequest<NotificationOperationResponse>;

public class MarkNotificationReadCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkNotificationReadCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(MarkNotificationReadCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var notification = await notificationRepository.GetByIdForUserAsync(request.NotificationId, userId, cancellationToken);
        if (notification is null)
        {
            return new NotificationOperationResponse { Success = false, Message = "Notification not found." };
        }

        await notificationRepository.MarkReadAsync(request.NotificationId, userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var unreadCount = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        return new NotificationOperationResponse
        {
            Success = true,
            Message = "Notification marked as read.",
            UnreadCount = unreadCount
        };
    }
}
