using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Client-facing actor identity must be omitted for anonymous chat and proposal content.
/// Kick-from-anonymous still uses the message/comment/proposal id on the server.
/// </summary>
public static class NotificationActorPrivacy
{
    public const string AnonymousActorName = "Anonymous";

    public static bool ShouldOmitActorIdentity(Notification notification) =>
        ShouldOmitActorIdentity(notification.Kind, notification.ActionUrl, notification.Title);

    public static bool ShouldOmitActorIdentity(NotificationKind kind, string? actionUrl, string? title)
    {
        if (IsAlwaysAnonymousKind(kind))
        {
            return true;
        }

        if ((kind is NotificationKind.Mention or NotificationKind.FleetMention) && IsProposalPath(actionUrl))
        {
            return true;
        }

        return IsPossiblyAnonymousChatKind(kind) && HasAnonymousActorTitle(title);
    }

    public static bool IsAlwaysAnonymousKind(NotificationKind kind) =>
        kind is NotificationKind.NewProposal
            or NotificationKind.NewFleetProposal
            or NotificationKind.NewProposalReply
            or NotificationKind.NewFleetProposalReply;

    private static bool IsPossiblyAnonymousChatKind(NotificationKind kind) =>
        kind is NotificationKind.NewChatMessage
            or NotificationKind.NewFleetChatMessage
            or NotificationKind.Mention
            or NotificationKind.FleetMention;

    private static bool IsProposalPath(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return false;
        }

        var path = actionUrl.Split('?')[0];
        return path.StartsWith("/app/crew/proposals", StringComparison.OrdinalIgnoreCase)
            || path.StartsWith("/app/fleet/proposals", StringComparison.OrdinalIgnoreCase);
    }

    private static bool HasAnonymousActorTitle(string? title) =>
        !string.IsNullOrWhiteSpace(title)
        && title.TrimStart().StartsWith(AnonymousActorName, StringComparison.OrdinalIgnoreCase);
}
