using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;

/// <summary>
/// Resolves which gift-event notification kind a viewer should receive for a single
/// recipient. At most one kind applies: to-me, then intermediary, then gifts-given.
/// Preference off means no notification for that role (no fallthrough).
/// </summary>
public static class GiftEventNotificationKinds
{
    public static NotificationKind? ResolveForViewer(
        int viewerUserId,
        int giverUserId,
        int recipientUserId,
        int? middlemanUserId,
        bool fleet)
    {
        if (viewerUserId == giverUserId)
        {
            return null;
        }

        if (viewerUserId == recipientUserId)
        {
            return fleet ? NotificationKind.FleetGiftReceivedByMe : NotificationKind.GiftReceivedByMe;
        }

        if (middlemanUserId is int middleman && viewerUserId == middleman)
        {
            return fleet ? NotificationKind.FleetIntermediaryGift : NotificationKind.IntermediaryGift;
        }

        return fleet ? NotificationKind.NewFleetGifts : NotificationKind.NewGifts;
    }

    public static CreateNotificationRequest BuildRequest(
        NotificationKind kind,
        int viewerUserId,
        int? crewId,
        string giverName,
        string recipientName,
        string actionUrl,
        int? relatedEntityId,
        int recipientUserId,
        int giverUserId,
        bool fleet)
    {
        var (title, body) = BuildCopy(kind, giverName, recipientName, fleet);
        return new CreateNotificationRequest
        {
            UserId = viewerUserId,
            CrewId = crewId,
            Kind = kind,
            Title = title,
            Body = body,
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId,
            SecondaryEntityId = recipientUserId,
            ActorUserId = giverUserId
        };
    }

    public static (string Title, string Body) BuildCopy(
        NotificationKind kind,
        string giverName,
        string recipientName,
        bool fleet)
    {
        var scope = fleet ? "fleet" : "crew";
        return kind switch
        {
            NotificationKind.GiftReceivedByMe or NotificationKind.FleetGiftReceivedByMe =>
                (
                    NotificationCopy.ReceivedGift(giverName, fleet),
                    $"{giverName} recorded a gift for you in your {scope}."
                ),
            NotificationKind.IntermediaryGift or NotificationKind.FleetIntermediaryGift =>
                (
                    NotificationCopy.IntermediaryGift(giverName, recipientName, fleet),
                    $"{giverName} recorded a gift for {recipientName} with you as the intermediary."
                ),
            _ =>
                (
                    NotificationCopy.GiftGiven(giverName, recipientName, fleet),
                    $"{giverName} recorded a gift for {recipientName} in your {scope}."
                )
        };
    }
}
