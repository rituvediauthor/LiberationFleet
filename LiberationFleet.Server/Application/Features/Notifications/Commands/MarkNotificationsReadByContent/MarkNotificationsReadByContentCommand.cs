using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.MarkNotificationsReadByContent;

public record MarkNotificationsReadByContentCommand(string? ActionUrlPrefix, int? RelatedEntityId)
    : IRequest<NotificationOperationResponse>;

public class MarkNotificationsReadByContentCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkNotificationsReadByContentCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(
        MarkNotificationsReadByContentCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        await notificationRepository.MarkReadByContentAsync(
            userId,
            request.ActionUrlPrefix,
            request.RelatedEntityId,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var unreadCount = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        return new NotificationOperationResponse
        {
            Success = true,
            Message = "Notifications marked as read.",
            UnreadCount = unreadCount
        };
    }
}
