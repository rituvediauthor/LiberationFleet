using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

public class NotificationService(
    INotificationRepository notificationRepository,
    INotificationRealtimeNotifier realtimeNotifier,
    NotificationBadgeSummaryService badgeSummaryService,
    IUnitOfWork unitOfWork)
{
    public async Task NotifyUserAsync(CreateNotificationRequest request, CancellationToken cancellationToken = default)
    {
        if (!await notificationRepository.IsKindEnabledAsync(request.UserId, request.Kind, cancellationToken))
        {
            return;
        }

        var notification = MapToEntity(request);
        await notificationRepository.AddAsync(notification, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var dto = NotificationMapper.Map(notification);
        await realtimeNotifier.NotifyReceivedAsync(request.UserId, dto, cancellationToken);
        await PushBadgeSummaryAsync(request.UserId, cancellationToken);
    }

    public async Task NotifyUsersAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var requestList = requests as IList<CreateNotificationRequest> ?? requests.ToList();
        if (requestList.Count == 0)
        {
            return;
        }

        // Batch preference lookups per kind instead of one query per recipient.
        var enabledRequests = new List<CreateNotificationRequest>(requestList.Count);
        foreach (var kindGroup in requestList.GroupBy(r => r.Kind))
        {
            var userIds = kindGroup.Select(r => r.UserId).Distinct().ToList();
            var disabledUserIds = await notificationRepository.GetUserIdsWithKindDisabledAsync(
                userIds,
                kindGroup.Key,
                cancellationToken);

            foreach (var request in kindGroup)
            {
                if (!disabledUserIds.Contains(request.UserId))
                {
                    enabledRequests.Add(request);
                }
            }
        }

        if (enabledRequests.Count == 0)
        {
            return;
        }

        var notifications = enabledRequests.Select(MapToEntity).ToList();
        await notificationRepository.AddRangeAsync(notifications, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            var dto = NotificationMapper.Map(notification);
            await realtimeNotifier.NotifyReceivedAsync(notification.UserId, dto, cancellationToken);
        }

        foreach (var userId in notifications.Select(n => n.UserId).Distinct())
        {
            await PushBadgeSummaryAsync(userId, cancellationToken);
        }
    }

    public async Task NotifyCrewAsync(
        int crewId,
        NotificationKind kind,
        string title,
        string body,
        string actionUrl,
        int? relatedEntityId = null,
        int? secondaryEntityId = null,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var userIds = await notificationRepository.GetCrewMemberUserIdsAsync(crewId, excludeUserId, cancellationToken);
        var requests = userIds.Select(userId => new CreateNotificationRequest
        {
            UserId = userId,
            CrewId = crewId,
            Kind = kind,
            Title = title,
            Body = body,
            ActionUrl = actionUrl,
            RelatedEntityId = relatedEntityId,
            SecondaryEntityId = secondaryEntityId,
            ActorUserId = excludeUserId
        });

        await NotifyUsersAsync(requests, cancellationToken);
    }

    public async Task NotifyCrewIfNotMutedAsync(
        int crewId,
        NotificationKind kind,
        MutedContentType muteType,
        int resourceId,
        string title,
        string body,
        string actionUrl,
        int? relatedEntityId = null,
        int? secondaryEntityId = null,
        int? excludeUserId = null,
        CancellationToken cancellationToken = default)
    {
        var userIds = await notificationRepository.GetCrewMemberUserIdsAsync(crewId, excludeUserId, cancellationToken);
        var mutedUserIds = await notificationRepository.GetUserIdsWithContentMutedAsync(
            userIds,
            muteType,
            resourceId,
            cancellationToken);

        var requests = userIds
            .Where(userId => !mutedUserIds.Contains(userId))
            .Select(userId => new CreateNotificationRequest
            {
                UserId = userId,
                CrewId = crewId,
                Kind = kind,
                Title = title,
                Body = body,
                ActionUrl = actionUrl,
                RelatedEntityId = relatedEntityId,
                SecondaryEntityId = secondaryEntityId,
                ActorUserId = excludeUserId
            });

        await NotifyUsersAsync(requests, cancellationToken);
    }

    public async Task PushBadgeSummaryAsync(int userId, CancellationToken cancellationToken = default)
    {
        _ = await PushBadgeSummaryAndGetAsync(userId, cancellationToken);
    }

    public async Task<NotificationBadgeSummaryResponse> PushBadgeSummaryAndGetAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var summary = await badgeSummaryService.GetForUserAsync(userId, cancellationToken);
        await realtimeNotifier.NotifyUnreadCountUpdatedAsync(userId, summary.UnreadCount, cancellationToken);
        await realtimeNotifier.NotifyBadgeSummaryUpdatedAsync(userId, summary, cancellationToken);
        return summary;
    }

    public static string GetKindLabel(NotificationKind kind) => kind switch
    {
        NotificationKind.NewProposal => "New proposal",
        NotificationKind.ProposalRejected => "Proposal rejected",
        NotificationKind.ProposalAccepted => "Proposal accepted",
        NotificationKind.NewGifts => "New gift(s)",
        NotificationKind.NewCycle => "New cycle",
        NotificationKind.NewSeason => "New season",
        NotificationKind.SurvivalThresholdsRefreshed => "Survival thresholds refreshed",
        NotificationKind.NewChatMessage => "New chat message",
        NotificationKind.NewReply => "New reply",
        NotificationKind.NewForumPost => "New post",
        NotificationKind.NewForumComment => "New comment",
        NotificationKind.NewCrewmate => "New crewmate",
        NotificationKind.JoinRequestFromPerson => "Join request",
        NotificationKind.JoinRequestFromCrew => "Crew invitation",
        NotificationKind.NewRule => "New rule",
        NotificationKind.RuleDeleted => "Rule deleted",
        NotificationKind.RuleEdited => "Rule edited",
        NotificationKind.CrewSettingChanged => "Crew setting changed",
        NotificationKind.CrewmateKicked => "Crewmate kicked",
        NotificationKind.CrewmateRejoinAllowed => "Crewmate may rejoin",
        NotificationKind.Mention => "Mention",
        NotificationKind.NewLibraryRequest => "New library request",
        NotificationKind.LibraryRequestDenied => "Library request denied",
        NotificationKind.LibraryRequestCompleted => "Library request completed",
        NotificationKind.NewLibraryRequestMessage => "Library request message",
        NotificationKind.LibraryUnitBrokenReported => "Library unit reported broken",
        NotificationKind.LibraryUnitBrokenConfirmed => "Library unit confirmed broken",
        NotificationKind.LibraryUnitReportedFixed => "Library unit reported fixed",
        NotificationKind.NewFleetGifts => "New fleet gift(s)",
        NotificationKind.NewFleetProposal => "New fleet proposal",
        NotificationKind.FleetSettingChanged => "Fleet setting changed",
        NotificationKind.NewFleetChatMessage => "New fleet chat message",
        NotificationKind.NewFleetForumPost => "New fleet post",
        NotificationKind.NewFleetForumComment => "New fleet comment",
        NotificationKind.NewEmergencyRequest => "Emergency request",
        NotificationKind.ForumPostLiked => "Post liked",
        NotificationKind.ForumCommentLiked => "Comment liked",
        NotificationKind.NewFleetRule => "New fleet rule",
        NotificationKind.FleetRuleDeleted => "Fleet rule deleted",
        NotificationKind.FleetRuleEdited => "Fleet rule edited",
        NotificationKind.FleetProposalAccepted => "Fleet proposal accepted",
        NotificationKind.FleetProposalRejected => "Fleet proposal rejected",
        NotificationKind.NewFleetReply => "New fleet reply",
        NotificationKind.FleetMention => "Fleet mention",
        NotificationKind.FleetForumPostLiked => "Fleet post liked",
        NotificationKind.FleetForumCommentLiked => "Fleet comment liked",
        NotificationKind.NewGiftComment => "New gift comment",
        NotificationKind.GiftEntryLiked => "Gift liked",
        NotificationKind.GiftCommentLiked => "Gift comment liked",
        NotificationKind.NewGiftReply => "New gift reply",
        NotificationKind.NewProposalReply => "New proposal reply",
        NotificationKind.NewFleetProposalReply => "New fleet proposal reply",
        NotificationKind.FriendRequest => "Friend request",
        NotificationKind.FriendRequestAccepted => "Friend request accepted",
        NotificationKind.NewDirectMessage => "New direct message",
        NotificationKind.ChatMessageLiked => "Message liked",
        NotificationKind.LibraryTaskScheduleChanged => "Quest schedule updated",
        _ => kind.ToString()
        };

    public static string GetKindCategory(NotificationKind kind) => kind switch
    {
        NotificationKind.NewFleetGifts
            or NotificationKind.NewFleetProposal
            or NotificationKind.FleetSettingChanged
            or NotificationKind.NewFleetChatMessage
            or NotificationKind.NewFleetForumPost
            or NotificationKind.NewFleetForumComment
            or NotificationKind.NewFleetRule
            or NotificationKind.FleetRuleDeleted
            or NotificationKind.FleetRuleEdited
            or NotificationKind.FleetProposalAccepted
            or NotificationKind.FleetProposalRejected
            or NotificationKind.NewFleetReply
            or NotificationKind.NewFleetProposalReply
            or NotificationKind.FleetMention
            or NotificationKind.FleetForumPostLiked
            or NotificationKind.FleetForumCommentLiked => "Fleet",
        NotificationKind.FriendRequest
            or NotificationKind.FriendRequestAccepted
            or NotificationKind.NewDirectMessage => "Friends",
        _ => "Crew"
    };

    private static Notification MapToEntity(CreateNotificationRequest request) => new()
    {
        UserId = request.UserId,
        CrewId = request.CrewId,
        Kind = request.Kind,
        Title = request.Title.Trim(),
        Body = NotificationPreview.Truncate(request.Body),
        ActionUrl = request.ActionUrl.Trim(),
        RelatedEntityId = request.RelatedEntityId,
        SecondaryEntityId = request.SecondaryEntityId,
        ActorUserId = request.ActorUserId,
        IsRead = false,
        CreatedAt = DateTime.UtcNow
    };
}
