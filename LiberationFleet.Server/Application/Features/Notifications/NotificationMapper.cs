using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class NotificationMapper
{
    public static NotificationDto Map(Notification notification) => new()
    {
        Id = notification.Id,
        CrewId = notification.CrewId,
        Kind = notification.Kind,
        Title = notification.Title,
        Body = notification.Body,
        ActionUrl = notification.ActionUrl,
        RelatedEntityId = notification.RelatedEntityId,
        SecondaryEntityId = notification.SecondaryEntityId,
        ActorUserId = notification.ActorUserId,
        IsRead = notification.IsRead,
        CreatedAt = notification.CreatedAt
    };
}
