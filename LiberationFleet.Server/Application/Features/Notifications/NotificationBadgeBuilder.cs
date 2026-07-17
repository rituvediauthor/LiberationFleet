using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

public static class NotificationBadgeBuilder
{
    private static readonly string[] AreaKeys =
    [
        "chats", "forums", "proposals", "giftLog", "rules", "settings", "library", "crewmates", "fleet"
    ];

    public static NotificationBadgeSummaryResponse Build(
        IReadOnlyList<Notification> notifications,
        IReadOnlyList<UserNotificationPreference> preferences,
        IReadOnlyList<UserMutedContent> mutedContents,
        IReadOnlyList<UserHiddenContent> hiddenContents,
        IReadOnlySet<int> hiddenUserIds)
    {
        var disabledKinds = preferences
            .Where(p => !p.IsEnabled)
            .Select(p => p.Kind)
            .ToHashSet();

        var mutedChatRoomIds = mutedContents
            .Where(m => m.ContentType == MutedContentType.ChatRoom)
            .Select(m => m.ResourceId)
            .ToHashSet();
        var mutedForumIds = mutedContents
            .Where(m => m.ContentType == MutedContentType.Forum)
            .Select(m => m.ResourceId)
            .ToHashSet();
        var mutedFriendIds = mutedContents
            .Where(m => m.ContentType == MutedContentType.Friend)
            .Select(m => m.ResourceId)
            .ToHashSet();
        var hiddenChatRoomIds = hiddenContents
            .Where(h => h.ContentType == MutedContentType.ChatRoom)
            .Select(h => h.ResourceId)
            .ToHashSet();
        var hiddenForumIds = hiddenContents
            .Where(h => h.ContentType == MutedContentType.Forum)
            .Select(h => h.ResourceId)
            .ToHashSet();

        var areaCounts = AreaKeys.ToDictionary(key => key, _ => 0);
        var resourceCounts = new Dictionary<string, int>();
        var visibleUnread = 0;

        foreach (var notification in notifications)
        {
            if (ShouldExclude(
                    notification,
                    disabledKinds,
                    mutedChatRoomIds,
                    mutedForumIds,
                    mutedFriendIds,
                    hiddenChatRoomIds,
                    hiddenForumIds,
                    hiddenUserIds))
            {
                continue;
            }

            visibleUnread++;

            var area = ResolveArea(notification);
            if (area is not null)
            {
                areaCounts[area]++;
            }

            foreach (var key in ResolveResourceKeys(notification))
            {
                resourceCounts[key] = resourceCounts.GetValueOrDefault(key) + 1;
            }
        }

        return new NotificationBadgeSummaryResponse
        {
            Success = true,
            Message = "Badge summary loaded.",
            UnreadCount = visibleUnread,
            AreaCounts = areaCounts,
            ResourceCounts = resourceCounts
        };
    }

    private static bool ShouldExclude(
        Notification notification,
        HashSet<NotificationKind> disabledKinds,
        HashSet<int> mutedChatRoomIds,
        HashSet<int> mutedForumIds,
        HashSet<int> mutedFriendIds,
        HashSet<int> hiddenChatRoomIds,
        HashSet<int> hiddenForumIds,
        IReadOnlySet<int> hiddenUserIds)
    {
        if (disabledKinds.Contains(notification.Kind))
        {
            return true;
        }

        if (notification.ActorUserId.HasValue && hiddenUserIds.Contains(notification.ActorUserId.Value))
        {
            return true;
        }

        if ((notification.Kind == NotificationKind.NewChatMessage
                || notification.Kind == NotificationKind.NewFleetChatMessage)
            && notification.RelatedEntityId.HasValue
            && (mutedChatRoomIds.Contains(notification.RelatedEntityId.Value)
                || hiddenChatRoomIds.Contains(notification.RelatedEntityId.Value)))
        {
            return true;
        }

        if (IsForumKind(notification.Kind)
            && notification.RelatedEntityId.HasValue
            && (mutedForumIds.Contains(notification.RelatedEntityId.Value)
                || hiddenForumIds.Contains(notification.RelatedEntityId.Value)))
        {
            return true;
        }

        if (notification.ActorUserId.HasValue && mutedFriendIds.Contains(notification.ActorUserId.Value))
        {
            return true;
        }

        return false;
    }

    private static bool IsForumKind(NotificationKind kind) =>
        kind is NotificationKind.NewForumPost
            or NotificationKind.NewForumComment
            or NotificationKind.NewReply
            or NotificationKind.Mention
            or NotificationKind.NewFleetForumPost
            or NotificationKind.NewFleetForumComment;

    private static string? ResolveArea(Notification notification)
    {
        var path = notification.ActionUrl.Split('?')[0];

        if (path.StartsWith("/app/crew/chats/", StringComparison.Ordinal)
            || path.StartsWith("/app/fleet/chats/", StringComparison.Ordinal))
        {
            return "chats";
        }

        if (path.StartsWith("/app/crew/forums/", StringComparison.Ordinal)
            || path.StartsWith("/app/fleet/forums/", StringComparison.Ordinal))
        {
            return "forums";
        }

        if (path.StartsWith("/app/crew/proposals", StringComparison.Ordinal)
            || path.StartsWith("/app/fleet/proposals", StringComparison.Ordinal))
        {
            return "proposals";
        }

        if (path.StartsWith("/app/crew/library-of-things", StringComparison.Ordinal)
            || path.StartsWith("/app/fleet/library", StringComparison.Ordinal))
        {
            return "library";
        }

        if (path.StartsWith("/app/crew/rules", StringComparison.Ordinal)
            || path.StartsWith("/app/fleet/rules", StringComparison.Ordinal))
        {
            return "rules";
        }

        if (path.Equals("/app/crew/edit", StringComparison.Ordinal)
            || path.Equals("/app/fleet/edit", StringComparison.Ordinal))
        {
            return "settings";
        }

        if (path.StartsWith("/app/crew/crewmates", StringComparison.Ordinal)
            || path.StartsWith("/app/fleet/crews", StringComparison.Ordinal))
        {
            return "crewmates";
        }

        if (path == "/app/crew/gift-log"
            || path.StartsWith("/app/crew/season-setup", StringComparison.Ordinal)
            || path.StartsWith("/app/crew/join-season", StringComparison.Ordinal)
            || path == "/app/fleet/gift-log"
            || path.StartsWith("/app/fleet/emergency", StringComparison.Ordinal))
        {
            return "giftLog";
        }

        if (path.StartsWith("/app/fleet/", StringComparison.Ordinal))
        {
            return "fleet";
        }

        return notification.Kind switch
        {
            NotificationKind.NewChatMessage or NotificationKind.NewFleetChatMessage => "chats",
            NotificationKind.NewForumPost or NotificationKind.NewForumComment or NotificationKind.NewReply
                or NotificationKind.NewFleetForumPost or NotificationKind.NewFleetForumComment => "forums",
            NotificationKind.NewProposal or NotificationKind.NewFleetProposal
                or NotificationKind.ProposalRejected or NotificationKind.ProposalAccepted => "proposals",
            NotificationKind.NewGifts or NotificationKind.NewCycle or NotificationKind.NewSeason
                or NotificationKind.SurvivalThresholdsRefreshed => "giftLog",
            NotificationKind.NewRule or NotificationKind.RuleDeleted or NotificationKind.RuleEdited => "rules",
            NotificationKind.CrewSettingChanged or NotificationKind.FleetSettingChanged => "settings",
            NotificationKind.NewCrewmate or NotificationKind.CrewmateKicked or NotificationKind.CrewmateRejoinAllowed
                or NotificationKind.JoinRequestFromPerson or NotificationKind.JoinRequestFromCrew => "crewmates",
            NotificationKind.NewLibraryRequest or NotificationKind.LibraryRequestDenied or NotificationKind.LibraryRequestCompleted
                or NotificationKind.NewLibraryRequestMessage or NotificationKind.LibraryUnitBrokenReported
                or NotificationKind.LibraryUnitBrokenConfirmed or NotificationKind.LibraryUnitReportedFixed => "library",
            NotificationKind.NewFleetGifts => "fleet",
            _ => null
        };
    }

    private static IEnumerable<string> ResolveResourceKeys(Notification notification)
    {
        var path = notification.ActionUrl.Split('?')[0];
        var keys = new List<string>();

        if (TryExtractPathId(path, "/app/crew/chats/", out var chatRoomId)
            || TryExtractPathId(path, "/app/fleet/chats/", out chatRoomId))
        {
            keys.Add($"chat:{chatRoomId}");
        }

        if (TryExtractPathId(path, "/app/crew/forums/", out var forumPostId)
            || TryExtractPathId(path, "/app/fleet/forums/", out forumPostId))
        {
            keys.Add($"forum:{forumPostId}");
        }
        else if (notification.RelatedEntityId.HasValue && IsForumKind(notification.Kind))
        {
            keys.Add($"forum:{notification.RelatedEntityId.Value}");
        }

        if (TryExtractPathId(path, "/app/crew/proposals/", out var proposalId)
            || TryExtractPathId(path, "/app/fleet/proposals/", out proposalId))
        {
            if (!path.Contains("/list/", StringComparison.Ordinal))
            {
                keys.Add($"proposal:{proposalId}");
            }
        }
        else if (notification.RelatedEntityId.HasValue && IsProposalKind(notification.Kind))
        {
            keys.Add($"proposal:{notification.RelatedEntityId.Value}");
        }

        if (path.Contains("/app/crew/proposals/list/", StringComparison.Ordinal)
            || path.Contains("/app/fleet/proposals/list/", StringComparison.Ordinal))
        {
            var status = path.Split('/').LastOrDefault()?.ToLowerInvariant();
            if (status is "approved" or "pending" or "rejected")
            {
                keys.Add($"proposal-status:{status}");
            }
        }
        else
        {
            var statusKey = notification.Kind switch
            {
                NotificationKind.NewProposal or NotificationKind.NewFleetProposal => "pending",
                NotificationKind.ProposalAccepted => "approved",
                NotificationKind.ProposalRejected => "rejected",
                _ => null
            };
            if (statusKey is not null)
            {
                keys.Add($"proposal-status:{statusKey}");
            }
        }

        if (path.StartsWith("/app/crew/library-of-things/requests/", StringComparison.Ordinal))
        {
            var segments = path.Split('/', StringSplitOptions.RemoveEmptyEntries);
            var requestsIndex = Array.IndexOf(segments, "requests");
            if (requestsIndex >= 0 && requestsIndex + 1 < segments.Length && int.TryParse(segments[requestsIndex + 1], out var requestId))
            {
                keys.Add($"library-request:{requestId}");
            }
        }

        switch (notification.Kind)
        {
            case NotificationKind.NewLibraryRequest:
                keys.Add("library-section:requests");
                break;
            case NotificationKind.LibraryRequestDenied:
            case NotificationKind.LibraryRequestCompleted:
                keys.Add("library-section:requests-mine");
                break;
            case NotificationKind.NewLibraryRequestMessage:
                if (notification.RelatedEntityId.HasValue)
                {
                    keys.Add($"library-request:{notification.RelatedEntityId.Value}");
                }
                break;
            case NotificationKind.LibraryUnitBrokenReported:
            case NotificationKind.LibraryUnitBrokenConfirmed:
            case NotificationKind.LibraryUnitReportedFixed:
                keys.Add("library-section:mine");
                break;
        }

        return keys.Distinct();
    }

    private static bool IsProposalKind(NotificationKind kind) =>
        kind is NotificationKind.NewProposal
            or NotificationKind.NewFleetProposal
            or NotificationKind.ProposalRejected
            or NotificationKind.ProposalAccepted
            or NotificationKind.NewReply;

    private static bool TryExtractPathId(string path, string prefix, out int id)
    {
        id = 0;
        if (!path.StartsWith(prefix, StringComparison.Ordinal))
        {
            return false;
        }

        var remainder = path[prefix.Length..];
        var segment = remainder.Split('/')[0];
        return int.TryParse(segment, out id);
    }
}
