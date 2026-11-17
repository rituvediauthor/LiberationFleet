using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Clears RequiresAction and marks related inbox rows read after the user completes
/// the linked workflow (gift confirm, intermediary transfer, quest confirm/deny).
/// </summary>
public static class ActionRequiredNotificationResolver
{
    public static async Task ResolveGiftForRecipientAsync(
        INotificationRepository notificationRepository,
        int recipientUserId,
        int giftId,
        bool fleet,
        CancellationToken cancellationToken = default)
    {
        // Fleet gifts reuse the same Gift row as crew gifts; clear both kind families.
        _ = fleet;
        await notificationRepository.ResolveRequiresActionAsync(
            recipientUserId,
            NotificationKind.GiftReceivedByMe,
            giftId,
            cancellationToken);
        await notificationRepository.ResolveRequiresActionAsync(
            recipientUserId,
            NotificationKind.FleetGiftReceivedByMe,
            giftId,
            cancellationToken);
    }

    public static async Task ResolveGiftForIntermediaryAsync(
        INotificationRepository notificationRepository,
        int intermediaryUserId,
        int giftId,
        bool fleet,
        CancellationToken cancellationToken = default)
    {
        _ = fleet;
        await notificationRepository.ResolveRequiresActionAsync(
            intermediaryUserId,
            NotificationKind.IntermediaryGift,
            giftId,
            cancellationToken);
        await notificationRepository.ResolveRequiresActionAsync(
            intermediaryUserId,
            NotificationKind.FleetIntermediaryGift,
            giftId,
            cancellationToken);
    }

    public static async Task ResolveGiftWorkflowAsync(
        INotificationRepository notificationRepository,
        int giftId,
        int recipientUserId,
        int? middlemanUserId,
        bool fleet,
        CancellationToken cancellationToken = default)
    {
        await ResolveGiftForRecipientAsync(
            notificationRepository,
            recipientUserId,
            giftId,
            fleet,
            cancellationToken);

        if (middlemanUserId is int middlemanId && middlemanId > 0)
        {
            await ResolveGiftForIntermediaryAsync(
                notificationRepository,
                middlemanId,
                giftId,
                fleet,
                cancellationToken);
        }
    }

    public static Task ResolveQuestCompletionAsync(
        INotificationRepository notificationRepository,
        int creatorUserId,
        int taskId,
        CancellationToken cancellationToken = default) =>
        notificationRepository.ResolveRequiresActionAsync(
            creatorUserId,
            NotificationKind.LibraryTaskCompletionSubmitted,
            taskId,
            cancellationToken);

    public static Task ResolveRelatedForAllUsersAsync(
        INotificationRepository notificationRepository,
        NotificationKind kind,
        int relatedEntityId,
        CancellationToken cancellationToken = default) =>
        notificationRepository.ResolveRequiresActionByRelatedAsync(
            kind,
            relatedEntityId,
            cancellationToken);
}
