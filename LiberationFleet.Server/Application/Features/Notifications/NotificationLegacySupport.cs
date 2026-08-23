using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Helpers for historical notification rows (pre–proposal-reply kind split, legacy URLs).
/// </summary>
public static class NotificationLegacySupport
{
    public static string PathPrefix(string? actionUrl)
    {
        if (string.IsNullOrWhiteSpace(actionUrl))
        {
            return string.Empty;
        }

        return actionUrl.Split('?')[0];
    }

    public static bool IsProposalActionUrl(string? actionUrl)
    {
        var path = PathPrefix(actionUrl);
        return path.Contains("/proposals", StringComparison.Ordinal);
    }

    public static bool IsLegacyProposalReplyKind(NotificationKind kind) =>
        kind is NotificationKind.NewReply or NotificationKind.NewFleetReply;

    public static bool IsProposalReplyNotification(Notification notification) =>
        IsLegacyProposalReplyKind(notification.Kind) && IsProposalActionUrl(notification.ActionUrl);

    /// <summary>
    /// Keep preference toggles aligned across the split reply kinds.
    /// </summary>
    public static HashSet<NotificationKind> ExpandDisabledKinds(IEnumerable<NotificationKind> disabled)
    {
        var set = disabled.ToHashSet();
        if (set.Contains(NotificationKind.NewReply))
        {
            set.Add(NotificationKind.NewProposalReply);
        }

        if (set.Contains(NotificationKind.NewProposalReply))
        {
            set.Add(NotificationKind.NewReply);
        }

        if (set.Contains(NotificationKind.NewFleetReply))
        {
            set.Add(NotificationKind.NewFleetProposalReply);
        }

        if (set.Contains(NotificationKind.NewFleetProposalReply))
        {
            set.Add(NotificationKind.NewFleetReply);
        }

        return set;
    }
}
