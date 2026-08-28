using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Gifts;

/// <summary>
/// Past-season gift entries are locked for verification/mutation except for accountants.
/// </summary>
public static class GiftSeasonAccess
{
    public static bool IsSeasonLocked(Gift gift, DateTime? currentSeasonStartDate, DateTime? giftSeasonStartDate)
    {
        if (!currentSeasonStartDate.HasValue)
        {
            return false;
        }

        var seasonStart = giftSeasonStartDate ?? gift.CreatedAt;
        return seasonStart < currentSeasonStartDate.Value;
    }

    public static bool CanMutateVerification(bool canBypassSeasonLock, bool isSeasonLocked) =>
        !isSeasonLocked || canBypassSeasonLock;
}
