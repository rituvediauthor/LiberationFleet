using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library.Commands.LibraryTasks;

internal static class LibraryTaskCompletionNotifications
{
    public static async Task NotifyCreatorAsync(
        NotificationService notificationService,
        INotificationRepository notificationRepository,
        IUserRepository userRepository,
        LibraryTask task,
        int completerUserId,
        CancellationToken cancellationToken)
    {
        if (task.CreatorUserId == completerUserId)
        {
            return;
        }

        // One action-required row per task while completions are pending.
        var existing = await notificationRepository.GetForUserByKindAndRelatedAsync(
            task.CreatorUserId,
            NotificationKind.LibraryTaskCompletionSubmitted,
            task.Id,
            cancellationToken);
        if (existing.Any(n => n.RequiresAction && !n.IsRead))
        {
            return;
        }

        var completer = await userRepository.GetByIdAsync(completerUserId, cancellationToken);
        var completerName = completer?.Username ?? "A crewmate";
        var pendingCount = task.Instances.Count(i =>
            i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation);
        var body = pendingCount > 1
            ? $"{completerName} marked a quest complete ({pendingCount} awaiting confirmation). Confirm or deny."
            : $"{completerName} marked a quest complete. Confirm or deny the completion.";

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = task.CreatorUserId,
            CrewId = task.CrewId,
            Kind = NotificationKind.LibraryTaskCompletionSubmitted,
            Title = "Quest completion submitted",
            Body = body,
            ActionUrl = $"/app/crew/library-of-things/tasks/{task.Id}",
            RelatedEntityId = task.Id,
            ActorUserId = completerUserId,
            RequiresAction = true
        }, cancellationToken);
    }

    public static async Task ResolveIfNoPendingAsync(
        INotificationRepository notificationRepository,
        LibraryTask task,
        CancellationToken cancellationToken)
    {
        var stillPending = task.Instances.Any(i =>
            i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation);
        if (stillPending)
        {
            return;
        }

        await ActionRequiredNotificationResolver.ResolveQuestCompletionAsync(
            notificationRepository,
            task.CreatorUserId,
            task.Id,
            cancellationToken);
    }
}
