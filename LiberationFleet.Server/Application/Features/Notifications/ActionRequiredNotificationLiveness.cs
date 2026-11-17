using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Decides whether an action-required notification still has an outstanding action.
/// When the linked content is deleted or the workflow is already finished, mark-read is allowed.
/// </summary>
public sealed class ActionRequiredNotificationLiveness(
    ILibraryTaskRepository taskRepository,
    IGiftRepository giftRepository,
    ILibraryRepository libraryRepository,
    ICrewInvitationRepository invitationRepository,
    IFriendshipRepository friendshipRepository)
{
    public async Task<bool> IsActionStillRequiredAsync(
        Notification notification,
        CancellationToken cancellationToken = default)
    {
        if (!notification.RequiresAction)
        {
            return false;
        }

        return notification.Kind switch
        {
            NotificationKind.LibraryTaskCompletionSubmitted =>
                await QuestStillNeedsConfirmAsync(notification.RelatedEntityId, cancellationToken),
            NotificationKind.GiftReceivedByMe or NotificationKind.FleetGiftReceivedByMe =>
                await RecipientGiftStillNeedsActionAsync(notification.RelatedEntityId, cancellationToken),
            NotificationKind.IntermediaryGift or NotificationKind.FleetIntermediaryGift =>
                await IntermediaryGiftStillNeedsActionAsync(notification.RelatedEntityId, cancellationToken),
            NotificationKind.NewLibraryRequest =>
                await LibraryRequestStillOpenAsync(notification.RelatedEntityId, cancellationToken),
            NotificationKind.JoinRequestFromCrew =>
                await InvitationStillPendingAsync(notification.RelatedEntityId, cancellationToken),
            NotificationKind.LibraryUnitBrokenReported =>
                await BrokenReportStillPendingAsync(notification.RelatedEntityId, cancellationToken),
            NotificationKind.FriendRequest =>
                await FriendRequestStillPendingAsync(
                    notification.UserId,
                    notification.ActorUserId,
                    cancellationToken),
            NotificationKind.CrewJoinSwitchOffer =>
                notification.SecondaryEntityId is null
                || notification.SecondaryEntityId == 0,
            _ => false
        };
    }

    private async Task<bool> QuestStillNeedsConfirmAsync(
        int? taskId,
        CancellationToken cancellationToken)
    {
        if (taskId is not int id)
        {
            return false;
        }

        var task = await taskRepository.GetTrackedTaskByIdAsync(id, cancellationToken);
        if (task is null || task.IsClosed)
        {
            return false;
        }

        return task.Instances.Any(i => i.Status == LibraryTaskInstanceStatus.AwaitingConfirmation);
    }

    private async Task<bool> RecipientGiftStillNeedsActionAsync(
        int? giftId,
        CancellationToken cancellationToken)
    {
        if (giftId is not int id)
        {
            return false;
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(id, cancellationToken);
        if (gift is null)
        {
            return false;
        }

        return gift.VerificationStatus is GiftVerificationStatus.Pending
            or GiftVerificationStatus.AwaitingRecipientVerification;
    }

    private async Task<bool> IntermediaryGiftStillNeedsActionAsync(
        int? giftId,
        CancellationToken cancellationToken)
    {
        if (giftId is not int id)
        {
            return false;
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(id, cancellationToken);
        if (gift is null || gift.Type != GiftType.Initiated)
        {
            return false;
        }

        return gift.VerificationStatus is GiftVerificationStatus.Pending
            or GiftVerificationStatus.MiddlemanReceivedFunds;
    }

    private async Task<bool> LibraryRequestStillOpenAsync(
        int? requestId,
        CancellationToken cancellationToken)
    {
        if (requestId is not int id)
        {
            return false;
        }

        var request = await libraryRepository.GetTrackedRequestByIdAsync(id, cancellationToken);
        return request is not null && request.Status == LibraryRequestStatus.Open;
    }

    private async Task<bool> InvitationStillPendingAsync(
        int? invitationId,
        CancellationToken cancellationToken)
    {
        if (invitationId is not int id)
        {
            return false;
        }

        var invitation = await invitationRepository.GetByIdAsync(id, cancellationToken);
        return invitation is not null && invitation.Status == CrewInvitationStatus.Pending;
    }

    private async Task<bool> BrokenReportStillPendingAsync(
        int? unitId,
        CancellationToken cancellationToken)
    {
        if (unitId is not int id)
        {
            return false;
        }

        var unit = await libraryRepository.GetTrackedUnitByIdAsync(id, cancellationToken);
        return unit is not null
            && !unit.IsRetired
            && unit.BrokenPendingConfirmation;
    }

    private async Task<bool> FriendRequestStillPendingAsync(
        int userId,
        int? actorUserId,
        CancellationToken cancellationToken)
    {
        if (actorUserId is not int actorId)
        {
            return false;
        }

        var friendship = await friendshipRepository.GetBetweenUsersAsync(userId, actorId, cancellationToken);
        return friendship is not null
            && friendship.Status == FriendshipStatus.Pending
            && friendship.AddresseeUserId == userId;
    }
}
