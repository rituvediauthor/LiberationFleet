using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public static class EmergencyRequestAccounting
{
    /// <summary>
    /// Need still lacking both confirmed gifts and active split coverage.
    /// Used to cap new splits / uncovered direct cash — not the UI "remaining" burn-down.
    /// </summary>
    public static decimal GetAmountUncovered(EmergencyRequest request) =>
        MutualAidCalculationService.CeilingToWholeDollar(
            Math.Max(0m, request.AmountNeeded - request.AmountReceived - request.AmountSplitCommitted));

    /// <summary>
    /// Need still awaiting confirmed gifts. Splits do not reduce this until their emergency
    /// cycles receive gifts (which credit <see cref="EmergencyRequest.AmountReceived"/>).
    /// </summary>
    public static decimal GetAmountRemainingToReceive(EmergencyRequest request) =>
        MutualAidCalculationService.CeilingToWholeDollar(
            Math.Max(0m, request.AmountNeeded - request.AmountReceived));

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

    /// <summary>
    /// Converts queue-funded emergency-segment reception into request receipts.
    /// Does not resize split segments or restore primary caps — the segment is being filled, not shrunk.
    /// </summary>
    public static void ApplyQueueFundedReceipt(
        EmergencyRequest request,
        SeasonCycle emergencySegment,
        decimal amountAppliedToCycle)
    {
        if (amountAppliedToCycle <= 0m || request.AmountReceived >= request.AmountNeeded)
        {
            return;
        }

        var credit = Math.Min(
            amountAppliedToCycle,
            Math.Max(0m, request.AmountNeeded - request.AmountReceived));
        if (credit <= 0m)
        {
            return;
        }

        request.AmountReceived += credit;

        var convert = Math.Min(credit, Math.Max(0m, request.AmountSplitCommitted));
        request.AmountSplitCommitted -= convert;

        if (convert > 0m)
        {
            var split = request.SplitOffers
                .Where(o => !o.IsCancelled && o.Amount > 0m && o.RequesterEmergencyCycleId == emergencySegment.Id)
                .OrderBy(o => o.CreatedAt)
                .FirstOrDefault()
                ?? OrderSplitOffersForShrink(request.SplitOffers).FirstOrDefault();

            if (split is not null)
            {
                var reduceBy = Math.Min(convert, split.Amount);
                split.Amount -= reduceBy;
                if (split.Amount <= 0m)
                {
                    split.Amount = 0m;
                    split.IsCancelled = true;
                }
            }
        }

        RefreshFulfilledStatus(request);
    }
}
