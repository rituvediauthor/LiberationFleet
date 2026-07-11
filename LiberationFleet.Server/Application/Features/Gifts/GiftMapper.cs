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
        IReadOnlyList<PaymentPlatformOptionDto>? completionPlatformOptions = null)
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
            GiverName = gift.GiverUser.Username,
            RecipientId = gift.RecipientUserId,
            RecipientName = gift.RecipientUser is null
                ? "Unknown"
                : GiftDisplayNames.GetRecipientName(gift.RecipientUser),
            MiddlemanId = gift.MiddlemanUserId,
            MiddlemanName = gift.MiddlemanUser?.Username,
            Amount = gift.Amount,
            Platform = gift.CrewPaymentPlatform.Name,
            Timestamp = gift.CreatedAt,
            Message = FormatMessage(gift, entryStatus, displayFlag),
            RelatedUserIds = relatedUserIds,
            Status = entryStatus,
            VerificationStatus = gift.VerificationStatus.ToString(),
            DisplayFlag = displayFlag,
            AvailableActions = availableActions,
            CompletionPlatformOptions = completionPlatformOptions is null
                ? Array.Empty<GiftPlatformOptionDto>()
                : completionPlatformOptions.Select(p => new GiftPlatformOptionDto { Id = p.Id, Name = p.Name }).ToList()
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
        Platform = gift.CrewPaymentPlatform.Name
    };

    private static string ResolveStatus(Gift gift, Gift? completedChild)
    {
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
            return gift.VerificationStatus == GiftVerificationStatus.Verified ? "completed" : "pending";
        }

        return gift.VerificationStatus == GiftVerificationStatus.Verified ? "completed" : "pending";
    }

    private static string FormatMessage(Gift gift, string status, string? displayFlag)
    {
        var amount = gift.Amount.ToString("0.##");
        var platform = gift.CrewPaymentPlatform.Name;

        var recipientName = gift.RecipientUser is null
            ? "Unknown"
            : GiftDisplayNames.GetRecipientName(gift.RecipientUser);
        var baseMessage = gift.Type switch
        {
            GiftType.Direct =>
                $"{gift.GiverUser.Username} gave ${amount} to {recipientName} via {platform}",
            GiftType.Initiated =>
                $"{gift.GiverUser.Username} initiated a ${amount} gift to {recipientName} through {gift.MiddlemanUser!.Username} via {platform}",
            GiftType.Completed =>
                $"{gift.MiddlemanUser!.Username} completed {gift.GiverUser.Username}'s ${amount} gift to {recipientName} via {platform.ToUpperInvariant()}",
            _ => string.Empty
        };

        if (displayFlag == GiftVerificationUiHelper.FlagNotComplete)
        {
            return $"{baseMessage} (Not Complete)";
        }

        if (displayFlag == GiftVerificationUiHelper.FlagCantComplete)
        {
            return $"{baseMessage} (Can't Complete)";
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
