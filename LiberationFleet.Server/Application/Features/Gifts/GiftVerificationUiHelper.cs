using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftVerificationUiHelper
{
    public const string ActionConfirmReceived = "confirmReceived";
    public const string ActionConfirmNotReceived = "confirmNotReceived";
    public const string ActionCompleteTransfer = "completeTransfer";
    public const string ActionCantComplete = "cantComplete";

    public const string FlagNotComplete = "notComplete";
    public const string FlagCantComplete = "cantComplete";
    public const string FlagUnverified = "unverified";

    public static string? GetDisplayFlag(Gift gift)
    {
        return gift.VerificationStatus switch
        {
            GiftVerificationStatus.TransferNotReceived => FlagNotComplete,
            GiftVerificationStatus.RecipientNotReceived => FlagNotComplete,
            GiftVerificationStatus.MiddlemanCannotComplete => FlagCantComplete,
            GiftVerificationStatus.Pending when gift.Type == GiftType.Direct => FlagUnverified,
            GiftVerificationStatus.AwaitingRecipientVerification => FlagUnverified,
            _ => null
        };
    }

    public static IReadOnlyList<string> GetAvailableActions(
        Gift gift,
        int viewerUserId,
        Gift? completedChild,
        Gift? initiatedParent,
        bool canRoleVerify = false)
    {
        if (gift.IsCustomGift
            || gift.Type is GiftType.SeasonStarted
                or GiftType.CycleStarted
                or GiftType.CycleCompleted
                or GiftType.SurvivalThresholdsRefreshed
            || gift.VerificationStatus is GiftVerificationStatus.Verified
                or GiftVerificationStatus.MiddlemanCannotComplete)
        {
            return Array.Empty<string>();
        }

        var actions = new List<string>();

        if (gift.Type == GiftType.Direct && viewerUserId == gift.RecipientUserId)
        {
            switch (gift.VerificationStatus)
            {
                case GiftVerificationStatus.Pending:
                    actions.Add(ActionConfirmReceived);
                    actions.Add(ActionConfirmNotReceived);
                    break;
                case GiftVerificationStatus.TransferNotReceived:
                    actions.Add(ActionConfirmReceived);
                    break;
            }
        }
        else if (gift.Type == GiftType.Initiated && viewerUserId == gift.MiddlemanUserId)
        {
            if (gift.VerificationStatus == GiftVerificationStatus.Pending)
            {
                actions.Add(ActionConfirmReceived);
                actions.Add(ActionConfirmNotReceived);
            }
            else if (gift.VerificationStatus == GiftVerificationStatus.TransferNotReceived)
            {
                actions.Add(ActionConfirmReceived);
            }
            else if (gift.VerificationStatus == GiftVerificationStatus.MiddlemanReceivedFunds
                && completedChild is null)
            {
                actions.Add(ActionCompleteTransfer);
                actions.Add(ActionCantComplete);
            }
            else if (completedChild?.VerificationStatus == GiftVerificationStatus.RecipientNotReceived
                && gift.VerificationStatus != GiftVerificationStatus.MiddlemanCannotComplete)
            {
                actions.Add(ActionCantComplete);
            }
        }
        else if (gift.Type == GiftType.Completed && viewerUserId == gift.RecipientUserId)
        {
            if (initiatedParent?.VerificationStatus != GiftVerificationStatus.MiddlemanCannotComplete)
            {
                switch (gift.VerificationStatus)
                {
                    case GiftVerificationStatus.AwaitingRecipientVerification:
                        actions.Add(ActionConfirmReceived);
                        actions.Add(ActionConfirmNotReceived);
                        break;
                    case GiftVerificationStatus.RecipientNotReceived:
                        actions.Add(ActionConfirmReceived);
                        break;
                }
            }
        }

        if (canRoleVerify)
        {
            AppendOrganizerAccountantVerifyActions(gift, actions);
        }

        return actions;
    }

    /// <summary>
    /// Organizers/accountants can confirm receipt on unverified direct and completed gifts.
    /// </summary>
    private static void AppendOrganizerAccountantVerifyActions(Gift gift, List<string> actions)
    {
        if (gift.Type == GiftType.Direct
            && gift.VerificationStatus is GiftVerificationStatus.Pending
                or GiftVerificationStatus.TransferNotReceived)
        {
            if (!actions.Contains(ActionConfirmReceived))
            {
                actions.Add(ActionConfirmReceived);
            }

            return;
        }

        if (gift.Type == GiftType.Completed
            && gift.VerificationStatus is GiftVerificationStatus.AwaitingRecipientVerification
                or GiftVerificationStatus.RecipientNotReceived)
        {
            if (!actions.Contains(ActionConfirmReceived))
            {
                actions.Add(ActionConfirmReceived);
            }
        }
    }

    public static bool RequiresVerification(Gift gift) =>
        !gift.IsCustomGift;
}
