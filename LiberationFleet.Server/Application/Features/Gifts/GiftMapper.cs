using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftMapper
{
    public static GiftLogEntryDto MapGift(
        Gift gift,
        bool canCompleteAsMiddleman = false,
        string? status = null,
        IReadOnlyList<PaymentPlatformOptionDto>? completionPlatformOptions = null)
    {
        var relatedUserIds = new List<int> { gift.GiverUserId, gift.RecipientUserId };
        if (gift.MiddlemanUserId.HasValue)
        {
            relatedUserIds.Add(gift.MiddlemanUserId.Value);
        }

        var entryStatus = status ?? (gift.Type == GiftType.Initiated ? "pending" : "completed");

        return new GiftLogEntryDto
        {
            Id = gift.Id,
            Type = gift.Type.ToString().ToLowerInvariant(),
            GiverId = gift.GiverUserId,
            GiverName = gift.GiverUser.Username,
            RecipientId = gift.RecipientUserId,
            RecipientName = gift.RecipientUser.Username,
            MiddlemanId = gift.MiddlemanUserId,
            MiddlemanName = gift.MiddlemanUser?.Username,
            Amount = gift.Amount,
            Platform = gift.CrewPaymentPlatform.Name,
            Timestamp = gift.CreatedAt,
            Message = FormatMessage(gift, entryStatus),
            RelatedUserIds = relatedUserIds,
            CanCompleteAsMiddleman = canCompleteAsMiddleman,
            Status = entryStatus,
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
        RecipientName = gift.RecipientUser.Username,
        Amount = gift.Amount,
        Platform = gift.CrewPaymentPlatform.Name
    };

    private static string FormatMessage(Gift gift, string status)
    {
        var amount = gift.Amount.ToString("0.##");
        var platform = gift.CrewPaymentPlatform.Name;

        var baseMessage = gift.Type switch
        {
            GiftType.Direct =>
                $"{gift.GiverUser.Username} gave ${amount} to {gift.RecipientUser.Username} via {platform}",
            GiftType.Initiated =>
                $"{gift.GiverUser.Username} initiated a ${amount} gift to {gift.RecipientUser.Username} through {gift.MiddlemanUser!.Username} via {platform}",
            GiftType.Completed =>
                $"{gift.MiddlemanUser!.Username} completed {gift.GiverUser.Username}'s ${amount} gift to {gift.RecipientUser.Username} via {platform.ToUpperInvariant()}",
            _ => string.Empty
        };

        if (gift.Type == GiftType.Initiated && status == "completed")
        {
            return $"{baseMessage} (Completed)";
        }

        if (gift.Type == GiftType.Initiated && status == "pending")
        {
            return $"{baseMessage} (Pending)";
        }

        return baseMessage;
    }
}
