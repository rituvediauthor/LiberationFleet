using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

public interface ICustomGiftRecordingService
{
    /// <summary>
    /// Records a custom gift for the selected category, splitting any amount beyond
    /// that category's remaining need into a separate Other gift.
    /// </summary>
    Task<(Gift? AppliedGift, Gift? OtherGift)> RecordAsync(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        int paymentPlatformId,
        int? middlemanId,
        CustomGiftCategory category,
        CancellationToken cancellationToken);
}
