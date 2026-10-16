using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class NotificationMapper
{
    public static NotificationDto Map(Notification notification)
    {
        var omitActor = NotificationActorPrivacy.ShouldOmitActorIdentity(notification);
        return new NotificationDto
        {
            Id = notification.Id,
            CrewId = notification.CrewId,
            Kind = notification.Kind,
            Title = notification.Title,
            Body = notification.Body,
            ActionUrl = notification.ActionUrl,
            RelatedEntityId = notification.RelatedEntityId,
            SecondaryEntityId = notification.SecondaryEntityId,
            ActorUserId = omitActor ? null : notification.ActorUserId,
            IsRead = notification.IsRead,
            CreatedAt = notification.CreatedAt
        };
    }
}
