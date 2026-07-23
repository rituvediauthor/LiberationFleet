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
        Gift? initiatedParent)
    {
        if (gift.IsCustomGift
            || gift.Type is GiftType.SeasonStarted or GiftType.CycleStarted or GiftType.SurvivalThresholdsRefreshed
            || gift.VerificationStatus is GiftVerificationStatus.Verified
                or GiftVerificationStatus.MiddlemanCannotComplete)
        {
            return Array.Empty<string>();
        }

        if (gift.Type == GiftType.Direct)
        {
            if (viewerUserId != gift.RecipientUserId)
            {
                return Array.Empty<string>();
            }

            return gift.VerificationStatus switch
            {
                GiftVerificationStatus.Pending =>
                    [ActionConfirmReceived, ActionConfirmNotReceived],
                GiftVerificationStatus.TransferNotReceived =>
                    [ActionConfirmReceived],
                _ => Array.Empty<string>()
            };
        }

        if (gift.Type == GiftType.Initiated)
        {
            if (viewerUserId != gift.MiddlemanUserId)
            {
                return Array.Empty<string>();
            }

            if (gift.VerificationStatus == GiftVerificationStatus.Pending)
            {
                return [ActionConfirmReceived, ActionConfirmNotReceived];
            }

            if (gift.VerificationStatus == GiftVerificationStatus.TransferNotReceived)
            {
                return [ActionConfirmReceived];
            }

            if (gift.VerificationStatus == GiftVerificationStatus.MiddlemanReceivedFunds
                && completedChild is null)
            {
                return [ActionCompleteTransfer, ActionCantComplete];
            }

            if (completedChild?.VerificationStatus == GiftVerificationStatus.RecipientNotReceived
                && gift.VerificationStatus != GiftVerificationStatus.MiddlemanCannotComplete)
            {
                return [ActionCantComplete];
            }

            return Array.Empty<string>();
        }

        if (gift.Type == GiftType.Completed)
        {
            if (viewerUserId != gift.RecipientUserId)
            {
                return Array.Empty<string>();
            }

            if (initiatedParent?.VerificationStatus == GiftVerificationStatus.MiddlemanCannotComplete)
            {
                return Array.Empty<string>();
            }

            return gift.VerificationStatus switch
            {
                GiftVerificationStatus.AwaitingRecipientVerification =>
                    [ActionConfirmReceived, ActionConfirmNotReceived],
                GiftVerificationStatus.RecipientNotReceived =>
                    [ActionConfirmReceived],
                _ => Array.Empty<string>()
            };
        }

        return Array.Empty<string>();
    }

    public static bool RequiresVerification(Gift gift) =>
        !gift.IsCustomGift;
}
