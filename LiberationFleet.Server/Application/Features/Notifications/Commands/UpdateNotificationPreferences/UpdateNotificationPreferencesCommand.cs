using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Commands.UpdateNotificationPreferences;

public record UpdateNotificationPreferencesCommand(
    IReadOnlyList<NotificationPreferenceDto> Preferences,
    string? SettingsPassword) : IRequest<NotificationOperationResponse>;

public class UpdateNotificationPreferencesCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    INotificationRepository notificationRepository,
    ISecurityRepository securityRepository,
    IPasswordHasher passwordHasher,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateNotificationPreferencesCommand, NotificationOperationResponse>
{
    public async Task<NotificationOperationResponse> Handle(UpdateNotificationPreferencesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return new NotificationOperationResponse { Success = false, Message = "User not found." };
        }

        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(user, request.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return new NotificationOperationResponse { Success = false, Message = lockCheck.Message };
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

        await SettingsLockHelper.RecordSettingsChangedAlertAsync(
            userId,
            "Notification",
            securityRepository,
            unitOfWork,
            cancellationToken);

        var unreadCount = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        return new NotificationOperationResponse
        {
            Success = true,
            Message = "Notification preferences saved.",
            UnreadCount = unreadCount
        };
    }
}
