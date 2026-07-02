using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

public class NotificationService(
    INotificationRepository notificationRepository,
    INotificationRealtimeNotifier realtimeNotifier,
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
        await PushUnreadCountAsync(request.UserId, cancellationToken);
    }

    public async Task NotifyUsersAsync(
        IEnumerable<CreateNotificationRequest> requests,
        CancellationToken cancellationToken = default)
    {
        var notifications = new List<Notification>();
        foreach (var request in requests)
        {
            if (!await notificationRepository.IsKindEnabledAsync(request.UserId, request.Kind, cancellationToken))
            {
                continue;
            }

            notifications.Add(MapToEntity(request));
        }

        if (notifications.Count == 0)
        {
            return;
        }

        await notificationRepository.AddRangeAsync(notifications, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        foreach (var notification in notifications)
        {
            var dto = NotificationMapper.Map(notification);
            await realtimeNotifier.NotifyReceivedAsync(notification.UserId, dto, cancellationToken);
            await PushUnreadCountAsync(notification.UserId, cancellationToken);
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
            SecondaryEntityId = secondaryEntityId
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
        var requests = new List<CreateNotificationRequest>();

        foreach (var userId in userIds)
        {
            if (await notificationRepository.IsContentMutedAsync(userId, muteType, resourceId, cancellationToken))
            {
                continue;
            }

            requests.Add(new CreateNotificationRequest
            {
                UserId = userId,
                CrewId = crewId,
                Kind = kind,
                Title = title,
                Body = body,
                ActionUrl = actionUrl,
                RelatedEntityId = relatedEntityId,
                SecondaryEntityId = secondaryEntityId
            });
        }

        await NotifyUsersAsync(requests, cancellationToken);
    }

    public static string GetKindLabel(NotificationKind kind) => kind switch
    {
        NotificationKind.NewProposal => "New proposal",
        NotificationKind.ProposalRejected => "Proposal rejected",
        NotificationKind.ProposalAccepted => "Proposal accepted",
        NotificationKind.NewGifts => "New gift(s)",
        NotificationKind.NewCycle => "New cycle",
        NotificationKind.NewSeason => "New season",
        NotificationKind.NewChatMessage => "New chat message",
        NotificationKind.NewReply => "New reply",
        NotificationKind.NewForumPost => "New forum post",
        NotificationKind.NewProjectPost => "New project post",
        NotificationKind.NewForumComment => "New forum comment",
        NotificationKind.NewProjectComment => "New project comment",
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
        _ => kind.ToString()
    };

    private static Notification MapToEntity(CreateNotificationRequest request) => new()
    {
        UserId = request.UserId,
        CrewId = request.CrewId,
        Kind = request.Kind,
        Title = request.Title.Trim(),
        Body = request.Body.Trim(),
        ActionUrl = request.ActionUrl.Trim(),
        RelatedEntityId = request.RelatedEntityId,
        SecondaryEntityId = request.SecondaryEntityId,
        IsRead = false,
        CreatedAt = DateTime.UtcNow
    };

    private async Task PushUnreadCountAsync(int userId, CancellationToken cancellationToken)
    {
        var unreadCount = await notificationRepository.GetUnreadCountAsync(userId, cancellationToken);
        await realtimeNotifier.NotifyUnreadCountUpdatedAsync(userId, unreadCount, cancellationToken);
    }
}
