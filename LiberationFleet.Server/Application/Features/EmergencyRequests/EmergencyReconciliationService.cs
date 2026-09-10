using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public sealed class EmergencyGiftReconciliationResult
{
    public decimal AmountAppliedToNeed { get; init; }
    public decimal OverflowAmount { get; init; }
    /// <summary>First open emergency cycle that received direct-gift credit, if any.</summary>
    public int? PrimarySeasonCycleId { get; init; }
}

public class EmergencyReconciliationService(IMutualAidRepository mutualAidRepository)
{
    /// <summary>
    /// Applies a direct gift to an open emergency request:
    /// 1) Fill open emergency-cycle segments first (confirmed gifts burn splits via those cycles).
    /// 2) Then credit remaining cash against need that is not covered by active splits.
    /// Splits alone never burn AmountReceived — only gift-funded cycle fill or uncovered cash does.
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
        var applied = 0m;
        int? primaryCycleId = null;

        var openSegments = await GetOpenEmergencySegmentsAsync(request, cancellationToken);
        foreach (var segment in openSegments)
        {
            if (remaining <= 0m)
            {
                break;
            }

            // Never fill more cycle room than the request still needs to receive — otherwise a
            // $50 gift on $40 remaining with $50 cycle room would swallow the $10 overflow.
            var requestRoom = EmergencyRequestAccounting.GetAmountRemainingToReceive(request);
            if (requestRoom <= 0m)
            {
                break;
            }

            var room = Math.Max(0m, segment.CycleCapAtStart - segment.CycleReceived);
            if (room <= 0m)
            {
                continue;
            }

            var take = Math.Min(remaining, Math.Min(room, requestRoom));
            segment.CycleReceived += take;
            if (segment.CycleReceived >= segment.CycleCapAtStart)
            {
                segment.CycleCompleted = true;
                segment.CycleCompletedAt ??= DateTime.UtcNow;
                segment.HasCycleStarted = false;
            }
            else
            {
                segment.HasCycleStarted = true;
            }

            EmergencyRequestAccounting.ApplyQueueFundedReceipt(request, segment, take);
            remaining -= take;
            applied += take;
            primaryCycleId ??= segment.Id;
        }

        if (remaining > 0m)
        {
            // Cash only burns need that splits do not already cover.
            var uncovered = EmergencyRequestAccounting.GetAmountUncovered(request);
            var toUncovered = Math.Min(remaining, uncovered);
            if (toUncovered > 0m)
            {
                request.AmountReceived += toUncovered;
                remaining -= toUncovered;
                applied += toUncovered;
            }
        }

        EmergencyRequestAccounting.RefreshFulfilledStatus(request);

        return new EmergencyGiftReconciliationResult
        {
            AmountAppliedToNeed = applied,
            OverflowAmount = remaining,
            PrimarySeasonCycleId = primaryCycleId
        };
    }

    private async Task<IReadOnlyList<SeasonCycle>> GetOpenEmergencySegmentsAsync(
        EmergencyRequest request,
        CancellationToken cancellationToken)
    {
        var crew = await mutualAidRepository.GetCrewAsync(request.CrewId, cancellationToken);
        if (crew?.CurrentSeasonStartDate is null)
        {
            return Array.Empty<SeasonCycle>();
        }

        var seasonDates = (await mutualAidRepository.GetSeasonStartDatesOnOrAfterAsync(
            request.CrewId,
            crew.CurrentSeasonStartDate.Value,
            cancellationToken)).ToList();
        if (!seasonDates.Contains(crew.CurrentSeasonStartDate.Value))
        {
            seasonDates.Insert(0, crew.CurrentSeasonStartDate.Value);
        }

        var segments = new List<SeasonCycle>();
        foreach (var seasonStart in seasonDates.Distinct().OrderBy(d => d))
        {
            var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
                request.CrewId,
                seasonStart,
                cancellationToken);
            segments.AddRange(cycles
                .Where(c => c.EmergencyRequestId == request.Id && !c.CycleCompleted)
                .OrderBy(c => c.ReceptionOrderPosition));
        }

        return segments;
    }
}
