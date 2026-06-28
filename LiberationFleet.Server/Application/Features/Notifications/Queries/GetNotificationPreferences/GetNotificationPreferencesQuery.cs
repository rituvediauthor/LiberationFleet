using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Notifications.Queries.GetNotificationPreferences;

public record GetNotificationPreferencesQuery() : IRequest<NotificationPreferencesResponse>;

public class GetNotificationPreferencesQueryHandler(
    ICurrentUserService currentUser,
    INotificationRepository notificationRepository) : IRequestHandler<GetNotificationPreferencesQuery, NotificationPreferencesResponse>
{
    public async Task<NotificationPreferencesResponse> Handle(GetNotificationPreferencesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new NotificationPreferencesResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var saved = await notificationRepository.GetPreferencesAsync(userId, cancellationToken);
        var preferences = Enum.GetValues<NotificationKind>()
            .Select(kind =>
            {
                var match = saved.FirstOrDefault(p => p.Kind == kind);
                return new NotificationPreferenceDto
                {
                    Kind = kind,
                    Label = NotificationService.GetKindLabel(kind),
                    IsEnabled = match?.IsEnabled ?? true
                };
            })
            .OrderBy(p => p.Label)
            .ToList();

        return new NotificationPreferencesResponse
        {
            Success = true,
            Message = "Notification preferences loaded.",
            Preferences = preferences
        };
    }
}
