using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftMapper
{
    public static GiftLogEntryDto MapGift(
        Gift gift,
        int? viewerUserId = null,
        Gift? completedChild = null,
        Gift? initiatedParent = null,
        string? status = null,
        IReadOnlyList<PaymentPlatformOptionDto>? completionPlatformOptions = null,
        int likeCount = 0,
        bool likedByCurrentUser = false,
        int commentCount = 0,
        bool isSeasonLocked = false,
        bool isAccountant = false)
    {
        var relatedUserIds = new List<int> { gift.GiverUserId, gift.RecipientUserId };
        if (gift.MiddlemanUserId.HasValue)
        {
            relatedUserIds.Add(gift.MiddlemanUserId.Value);
        }

        var entryStatus = status ?? ResolveStatus(gift, completedChild);
        var displayFlag = GiftVerificationUiHelper.GetDisplayFlag(gift);
        IReadOnlyList<string> availableActions = Array.Empty<string>();

        if (viewerUserId.HasValue)
        {
            availableActions = GiftVerificationUiHelper.GetAvailableActions(
                gift,
                viewerUserId.Value,
                completedChild,
                initiatedParent);
        }

        if (isSeasonLocked && !isAccountant)
        {
            availableActions = Array.Empty<string>();
        }

        if (viewerUserId.HasValue
            && gift.Type == GiftType.Initiated
            && gift.MiddlemanUserId == viewerUserId
            && completedChild is null
            && gift.VerificationStatus == GiftVerificationStatus.MiddlemanReceivedFunds
            && completionPlatformOptions is null
            && gift.MiddlemanUser is not null
            && gift.RecipientUser is not null)
        {
            completionPlatformOptions = CrewPaymentPlatformService.GetCommonPlatforms(gift.MiddlemanUser, gift.RecipientUser);
        }

        return new GiftLogEntryDto
        {
            Id = gift.Id,
            Type = gift.Type.ToString().ToLowerInvariant(),
            GiverId = gift.GiverUserId,
            GiverName = gift.GiverUser?.Username ?? string.Empty,
            RecipientId = gift.RecipientUserId,
            RecipientName = gift.RecipientUser is null
                ? "Unknown"
                : GiftDisplayNames.GetRecipientName(gift.RecipientUser),
            MiddlemanId = gift.MiddlemanUserId,
            MiddlemanName = gift.MiddlemanUser?.Username,
            Amount = gift.Amount,
            Platform = gift.CrewPaymentPlatform?.Name ?? string.Empty,
            Timestamp = gift.CreatedAt,
            Message = FormatMessage(gift, entryStatus, displayFlag),
            RelatedUserIds = relatedUserIds,
            Status = entryStatus,
            VerificationStatus = gift.VerificationStatus.ToString(),
            DisplayFlag = displayFlag,
            CustomGiftCategory = gift.CustomGiftCategory.HasValue
                ? CustomGiftRecordingService.ToApiValue(gift.CustomGiftCategory.Value)
                : null,
            AvailableActions = availableActions,
            CompletionPlatformOptions = completionPlatformOptions is null
                ? Array.Empty<GiftPlatformOptionDto>()
                : completionPlatformOptions.Select(p => new GiftPlatformOptionDto { Id = p.Id, Name = p.Name }).ToList(),
            LikeCount = likeCount,
            LikedByCurrentUser = likedByCurrentUser,
            CommentCount = commentCount,
            IsSeasonLocked = isSeasonLocked
        };
    }

    public static PendingMiddlemanGiftDto MapPendingGift(Gift gift) => new()
    {
        Id = gift.Id,
        InitiatorId = gift.GiverUserId,
        InitiatorName = gift.GiverUser.Username,
        RecipientId = gift.RecipientUserId,
        RecipientName = GiftDisplayNames.GetRecipientName(gift.RecipientUser),
        Amount = gift.Amount,
        Platform = gift.CrewPaymentPlatform?.Name
    };

    private static string ResolveStatus(Gift gift, Gift? completedChild)
    {
        if (IsCelebratory(gift.Type))
        {
            return "completed";
        }

        if (gift.Type == GiftType.Initiated)
        {
            if (gift.VerificationStatus == GiftVerificationStatus.MiddlemanCannotComplete)
            {
                return "cantComplete";
            }

            return completedChild is not null ? "completed" : "pending";
        }

        if (gift.Type == GiftType.Completed)
        {
            return gift.VerificationStatus == GiftVerificationStatus.Verified ? "completed" : "unverified";
        }

        if (gift.VerificationStatus == GiftVerificationStatus.Verified)
        {
            return "completed";
        }

        if (gift.VerificationStatus is GiftVerificationStatus.Pending
            or GiftVerificationStatus.AwaitingRecipientVerification)
        {
            return "unverified";
        }

        return "pending";
    }

    private static bool IsCelebratory(GiftType type) =>
        type is GiftType.SeasonStarted
            or GiftType.CycleStarted
            or GiftType.CycleCompleted
            or GiftType.SurvivalThresholdsRefreshed;

    private static string FormatMessage(Gift gift, string status, string? displayFlag)
    {
        var celebratoryMessage = gift.Type switch
        {
            GiftType.SeasonStarted =>
                "A new mutual aid season has begun!",
            GiftType.CycleStarted =>
                gift.RecipientUser is null
                    ? "A new reception cycle has started!"
                    : $"A new reception cycle has started for {GiftDisplayNames.GetRecipientName(gift.RecipientUser)}!",
            GiftType.CycleCompleted =>
                gift.RecipientUser is null
                    ? "A reception cycle has concluded!"
                    : $"{GiftDisplayNames.GetRecipientName(gift.RecipientUser)}'s reception cycle has concluded!",
            GiftType.SurvivalThresholdsRefreshed =>
                "Survival thresholds have refreshed for the new month!",
            _ => null
        };
        if (celebratoryMessage is not null)
        {
            return celebratoryMessage;
        }

        var amount = gift.Amount.ToString("0.##");
        var platform = gift.CrewPaymentPlatform?.Name ?? "unknown platform";
        var middlemanName = gift.MiddlemanUser?.Username ?? "a middleman";

        var recipientName = gift.RecipientUser is null
            ? "Unknown"
            : GiftDisplayNames.GetRecipientName(gift.RecipientUser);
        var giverName = gift.GiverUser?.Username ?? "Someone";
        string baseMessage;
        if (gift.CrewPaymentPlatform?.IsLibraryOfThings == true
            && !string.IsNullOrWhiteSpace(gift.LibraryItemTitle))
        {
            baseMessage = $"{giverName} gave ${amount} in {gift.LibraryItemTitle.Trim()} to {recipientName} via the Library of Things";
        }
        else
        {
            baseMessage = gift.Type switch
            {
                GiftType.Direct =>
                    $"{giverName} gave ${amount} to {recipientName} via {platform}",
                GiftType.Initiated =>
                    $"{giverName} initiated a ${amount} gift to {recipientName} through {middlemanName} via {platform}",
                GiftType.Completed =>
                    $"{middlemanName} completed {giverName}'s ${amount} gift to {recipientName} via {platform.ToUpperInvariant()}",
                _ => string.Empty
            };
        }

        if (gift.CustomGiftCategory.HasValue)
        {
            var label = CustomGiftRecordingService.ToDisplayLabel(gift.CustomGiftCategory.Value);
            baseMessage = $"{baseMessage} [{label}]";
        }

        if (displayFlag == GiftVerificationUiHelper.FlagNotComplete)
        {
            return $"{baseMessage} (Not Complete)";
        }

        if (displayFlag == GiftVerificationUiHelper.FlagCantComplete)
        {
            return $"{baseMessage} (Can't Complete)";
        }

        if (displayFlag == GiftVerificationUiHelper.FlagUnverified
            || status == "unverified")
        {
            return $"{baseMessage} (Unverified)";
        }

        if (gift.Type == GiftType.Initiated && status == "completed")
        {
            return $"{baseMessage} (Completed)";
        }

        if (gift.Type == GiftType.Initiated && status == "pending")
        {
            return $"{baseMessage} (Pending)";
        }

        if (gift.Type == GiftType.Completed && status == "pending")
        {
            return $"{baseMessage} (Awaiting confirmation)";
        }

        return baseMessage;
    }
}
