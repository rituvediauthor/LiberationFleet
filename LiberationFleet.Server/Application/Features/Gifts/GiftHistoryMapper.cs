using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

public static class GiftHistoryMapper
{
    public static GiftHistoryEntryDto MapEntry(Gift gift) => new()
    {
        Id = gift.Id,
        Amount = gift.Amount,
        Timestamp = gift.CreatedAt,
        GiftType = gift.Type.ToString().ToLowerInvariant(),
        Platform = gift.CrewPaymentPlatform?.Name ?? string.Empty,
        MiddlemanUsername = gift.MiddlemanUser?.Username,
        StatusLabel = BuildStatusLabel(gift)
    };

    private static string BuildStatusLabel(Gift gift)
    {
        return gift.Type switch
        {
            GiftType.Direct when gift.VerificationStatus == GiftVerificationStatus.Verified => "Completed",
            GiftType.Direct => "Awaiting confirmation",
            GiftType.Initiated when gift.VerificationStatus == GiftVerificationStatus.MiddlemanCannotComplete => "Could not complete",
            GiftType.Initiated => "Pending middleman",
            GiftType.Completed when gift.VerificationStatus == GiftVerificationStatus.Verified => "Completed",
            GiftType.Completed => "Awaiting recipient confirmation",
            _ => "Recorded"
        };
    }
}
