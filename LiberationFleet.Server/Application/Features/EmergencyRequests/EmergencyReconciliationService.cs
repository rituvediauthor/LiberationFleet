using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public sealed class EmergencyGiftReconciliationResult
{
    public decimal AmountAppliedToNeed { get; init; }
    public decimal OverflowAmount { get; init; }
}

public class EmergencyReconciliationService(EmergencySplitService splitService)
{
    /// <summary>
    /// Applies a direct gift amount to an open emergency request: cover uncovered need first,
    /// shrink active splits (runner-up before active cycle), then return any overflow for uncategorized recording.
    /// </summary>
    public async Task<EmergencyGiftReconciliationResult> ApplyDirectGiftAsync(
        EmergencyRequest request,
        decimal giftAmount,
        CancellationToken cancellationToken = default)
    {
        if (giftAmount <= 0m)
        {
            return new EmergencyGiftReconciliationResult();
        }

        if (request.AmountReceived >= request.AmountNeeded)
        {
            return new EmergencyGiftReconciliationResult
            {
                AmountAppliedToNeed = 0m,
                OverflowAmount = giftAmount
            };
        }

        var remaining = giftAmount;
        var uncovered = EmergencyRequestAccounting.GetAmountUncovered(request);
        var toUncovered = Math.Min(remaining, uncovered);
        request.AmountReceived += toUncovered;
        remaining -= toUncovered;

        if (remaining > 0m)
        {
            remaining = await splitService.ShrinkActiveSplitsAsync(request, remaining, cancellationToken);
        }

        EmergencyRequestAccounting.RefreshFulfilledStatus(request);

        return new EmergencyGiftReconciliationResult
        {
            AmountAppliedToNeed = giftAmount - remaining,
            OverflowAmount = remaining
        };
    }
}
