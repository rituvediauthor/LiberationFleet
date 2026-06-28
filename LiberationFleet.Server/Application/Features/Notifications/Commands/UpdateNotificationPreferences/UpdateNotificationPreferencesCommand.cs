using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.UpdateNotificationPreferences;

public record UpdateNotificationPreferencesCommand(IReadOnlyList<NotificationPreferenceDto> Preferences)
    : IRequest<NotificationOperationResponse>;

public class UpdateNotificationPreferencesCommandHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var preferences = request.Preferences
            .Select(p => new UserNotificationPreference
            {
                Kind = p.Kind,
                IsEnabled = p.IsEnabled
            })
            .ToList();

        await notificationRepository.UpsertPreferencesAsync(userId, preferences, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var unreadCount = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        return new NotificationOperationResponse
        {
            Success = true,
            Message = "Notification preferences saved.",
            UnreadCount = unreadCount
        };
    }
}
