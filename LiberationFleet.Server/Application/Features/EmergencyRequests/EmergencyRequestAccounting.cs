using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public static class EmergencyRequestAccounting
{
    public static decimal GetAmountUncovered(EmergencyRequest request) =>
        Math.Max(0m, request.AmountNeeded - request.AmountReceived - request.AmountSplitCommitted);

    public static decimal GetAmountRemainingToReceive(EmergencyRequest request) =>
        Math.Max(0m, request.AmountNeeded - request.AmountReceived);

    public static void RefreshFulfilledStatus(EmergencyRequest request)
    {
        if (request.Status == EmergencyRequestStatus.Cancelled)
        {
            return;
        }

        request.Status = request.AmountReceived >= request.AmountNeeded
            ? EmergencyRequestStatus.Fulfilled
            : EmergencyRequestStatus.Open;
    }

    public static IReadOnlyList<EmergencySplitOffer> GetActiveSplitOffers(EmergencyRequest request) =>
        request.SplitOffers
            .Where(o => !o.IsCancelled && o.Amount > 0m)
            .ToList();

    /// <summary>
    /// Runner-up splits shrink before active-cycle splits; oldest first within the same role.
    /// </summary>
    public static IEnumerable<EmergencySplitOffer> OrderSplitOffersForShrink(IEnumerable<EmergencySplitOffer> offers) =>
        offers
            .Where(o => !o.IsCancelled && o.Amount > 0m)
            .OrderBy(o => o.OffererQueueRole)
            .ThenBy(o => o.CreatedAt);
}
