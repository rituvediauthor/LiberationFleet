using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests;

public sealed class EmergencySplitResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;

    public static EmergencySplitResult Succeeded(string message) =>
        new() { Success = true, Message = message };

    public static EmergencySplitResult Failed(string message) =>
        new() { Success = false, Message = message };
}

public sealed class EmergencySplitEligibility
{
    public bool CanSplit { get; init; }
    public decimal MaxSplitAmount { get; init; }
    public IReadOnlyList<int> EligibleOffererUserIds { get; init; } = Array.Empty<int>();
    public string Message { get; init; } = string.Empty;
}

public class EmergencySplitService(
    IMutualAidRepository mutualAidRepository,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IMutualAidService mutualAidService)
{
    public async Task<IReadOnlyList<int>> CaptureEligibleOffererUserIdsAsync(
        int crewId,
        int requesterUserId,
        CancellationToken cancellationToken)
    {
        await mutualAidService.EnsureNextSeasonCyclesAsync(crewId, cancellationToken);
        var lockedUserIds = await mutualAidService.GetLockedCycleUserIdsAsync(crewId, cancellationToken);
        return lockedUserIds.Where(id => id != requesterUserId).ToList();
    }

    public async Task<EmergencySplitEligibility> GetViewerSplitEligibilityAsync(
        EmergencyRequest request,
        int viewerUserId,
        CancellationToken cancellationToken)
    {
        if (request.Status != EmergencyRequestStatus.Open || request.RequesterUserId == viewerUserId)
        {
            return new EmergencySplitEligibility
            {
                CanSplit = false,
                Message = "Split is not available for this request."
            };
        }

        if (!IsOffererEligibleForRequest(request, viewerUserId))
        {
            // Legacy requests without a snapshot: fall back to live queue order.
            if (!HasEligibilitySnapshot(request))
            {
                await mutualAidService.EnsureNextSeasonCyclesAsync(request.CrewId, cancellationToken);
                var lockedUserIds = await mutualAidService.GetLockedCycleUserIdsAsync(
                    request.CrewId,
                    cancellationToken);
                if (!lockedUserIds.Contains(viewerUserId) || viewerUserId == request.RequesterUserId)
                {
                    return new EmergencySplitEligibility
                    {
                        CanSplit = false,
                        Message = "Only the current and next cycle recipients can split aid for an emergency request."
                    };
                }
            }
            else
            {
                return new EmergencySplitEligibility
                {
                    CanSplit = false,
                    Message = "Only the current and next cycle recipients can split aid for an emergency request."
                };
            }
        }

        var uncovered = EmergencyRequestAccounting.GetAmountUncovered(request);
        if (uncovered <= 0m)
        {
            return new EmergencySplitEligibility
            {
                CanSplit = false,
                Message = "This emergency request has no uncovered need for additional cycle splits."
            };
        }

        var offererRemaining = await GetOffererRemainingAsync(request.CrewId, viewerUserId, cancellationToken);
        if (offererRemaining <= 0m)
        {
            return new EmergencySplitEligibility
            {
                CanSplit = false,
                Message = "You do not have remaining cycle capacity to split."
            };
        }

        return new EmergencySplitEligibility
        {
            CanSplit = true,
            MaxSplitAmount = Math.Min(uncovered, offererRemaining),
            EligibleOffererUserIds = ParseEligibleUserIds(request.SplitEligibleOffererUserIds)
        };
    }

    /// <summary>
    /// Shrinks active splits by up to <paramref name="amount"/> (runner-up splits first).
    /// Returns any amount that could not be applied to split reduction.
    /// </summary>
    public async Task<decimal> ShrinkActiveSplitsAsync(
        EmergencyRequest request,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            return 0m;
        }

        var remaining = amount;
        foreach (var split in EmergencyRequestAccounting.OrderSplitOffersForShrink(request.SplitOffers))
        {
            if (remaining <= 0m)
            {
                break;
            }

            var shrinkBy = Math.Min(remaining, split.Amount);
            var newAmount = split.Amount - shrinkBy;
            await ResizeSplitOfferAsync(request, split, newAmount, cancellationToken);
            request.AmountSplitCommitted -= shrinkBy;
            remaining -= shrinkBy;
        }

        return remaining;
    }

    public async Task<EmergencyOffererQueueRole> ResolveOffererQueueRoleAsync(
        int crewId,
        int offererUserId,
        CancellationToken cancellationToken)
    {
        var lockedUserIds = await mutualAidService.GetLockedCycleUserIdsAsync(crewId, cancellationToken);
        if (lockedUserIds.Count > 1 && lockedUserIds[1] == offererUserId)
        {
            return EmergencyOffererQueueRole.RunnerUp;
        }

        return EmergencyOffererQueueRole.ActiveCycle;
    }

    public async Task<EmergencySplitResult> ApplySplitAsync(
        EmergencyRequest request,
        int offererUserId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        if (amount <= 0)
        {
            return EmergencySplitResult.Failed("Split amount must be greater than zero.");
        }

        var uncovered = EmergencyRequestAccounting.GetAmountUncovered(request);
        if (amount > uncovered)
        {
            return EmergencySplitResult.Failed("Split amount exceeds the uncovered emergency need.");
        }

        var crew = await mutualAidRepository.GetCrewAsync(request.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return EmergencySplitResult.Failed("An active season is required to split a cycle.");
        }

        var offererMembership = await membershipRepository.GetMembershipAsync(
            offererUserId,
            request.CrewId,
            cancellationToken);
        if (offererMembership is null || offererMembership.IsBanned || !offererMembership.IsInSeason)
        {
            return EmergencySplitResult.Failed("You must be in the active season to offer a cycle split.");
        }

        var requesterMembership = await membershipRepository.GetMembershipAsync(
            request.RequesterUserId,
            request.CrewId,
            cancellationToken);
        if (requesterMembership is null || requesterMembership.IsBanned || !requesterMembership.IsInSeason)
        {
            return EmergencySplitResult.Failed("The requester must be in the active season.");
        }

        await mutualAidService.EnsureNextSeasonCyclesAsync(request.CrewId, cancellationToken);
        crew = await mutualAidRepository.GetCrewAsync(request.CrewId, cancellationToken) ?? crew;
        if (!crew.CurrentSeasonStartDate.HasValue)
        {
            return EmergencySplitResult.Failed("An active season is required to split a cycle.");
        }

        if (!IsOffererEligibleForRequest(request, offererUserId))
        {
            if (!HasEligibilitySnapshot(request))
            {
                var lockedUserIds = await mutualAidService.GetLockedCycleUserIdsAsync(
                    request.CrewId,
                    cancellationToken);
                if (!lockedUserIds.Contains(offererUserId) || offererUserId == request.RequesterUserId)
                {
                    return EmergencySplitResult.Failed(
                        "Only the current and next cycle recipients can split aid for an emergency request.");
                }
            }
            else
            {
                return EmergencySplitResult.Failed(
                    "Only the current and next cycle recipients can split aid for an emergency request.");
            }
        }

        var currentSeasonStart = crew.CurrentSeasonStartDate.Value;
        var seasonDates = (await mutualAidRepository.GetSeasonStartDatesOnOrAfterAsync(
            request.CrewId,
            currentSeasonStart,
            cancellationToken)).ToList();
        if (!seasonDates.Contains(currentSeasonStart))
        {
            seasonDates.Insert(0, currentSeasonStart);
        }

        var cyclesBySeason = new Dictionary<DateTime, List<SeasonCycle>>();
        foreach (var seasonStart in seasonDates.Distinct().OrderBy(d => d))
        {
            cyclesBySeason[seasonStart] = (await mutualAidRepository.GetSeasonCyclesAsync(
                request.CrewId,
                seasonStart,
                cancellationToken)).ToList();
        }

        var frozenCapacity = await BuildCapacityContextAsync(crew, useFrozenSeasonCaps: true, cancellationToken);
        var liveCapacity = await BuildCapacityContextAsync(crew, useFrozenSeasonCaps: false, cancellationToken);

        var offererIsMember = await mutualAidService.IsFinancialMemberAsync(
            offererUserId,
            crew.Id,
            offererMembership,
            cancellationToken);
        var requesterIsMember = await mutualAidService.IsFinancialMemberAsync(
            request.RequesterUserId,
            crew.Id,
            requesterMembership,
            cancellationToken);

        // Re-check remaining against live cycle state at submit time (gifts may have landed while the form was open).
        var (offererPrimary, offererSeasonCycles, offererCapacity) = FindPrimaryAcrossSeasons(
            cyclesBySeason,
            offererUserId,
            offererIsMember,
            frozenCapacity,
            liveCapacity);
        if (offererPrimary is null || offererSeasonCycles is null || offererCapacity is null)
        {
            return EmergencySplitResult.Failed("You do not have an active cycle segment to split from.");
        }

        var offererRemaining = GetSegmentRemaining(offererPrimary, offererCapacity, offererIsMember);
        if (amount > offererRemaining)
        {
            return EmergencySplitResult.Failed($"You can split at most ${offererRemaining:0.##} from your cycle.");
        }

        var (requesterPrimary, requesterSeasonCycles, requesterCapacity) = FindPrimaryAcrossSeasons(
            cyclesBySeason,
            request.RequesterUserId,
            requesterIsMember,
            frozenCapacity,
            liveCapacity);
        if (requesterPrimary is null || requesterSeasonCycles is null || requesterCapacity is null)
        {
            return EmergencySplitResult.Failed("The requester does not have an active cycle segment.");
        }

        var requesterRemaining = GetSegmentRemaining(requesterPrimary, requesterCapacity, requesterIsMember);
        if (amount > requesterRemaining)
        {
            return EmergencySplitResult.Failed("The requester does not have enough cycle capacity for this split.");
        }

        var offererQueueRole = await ResolveOffererQueueRoleAsync(request.CrewId, offererUserId, cancellationToken);
        var splitOffer = new EmergencySplitOffer
        {
            EmergencyRequest = request,
            OffererUserId = offererUserId,
            Amount = amount,
            OffererQueueRole = offererQueueRole,
            CreatedAt = DateTime.UtcNow
        };
        await emergencyRequestRepository.AddSplitOfferAsync(splitOffer, cancellationToken);

        ReduceSegmentCap(offererPrimary, amount, offererCapacity, offererIsMember);
        ReduceSegmentCap(requesterPrimary, amount, requesterCapacity, requesterIsMember);

        // Emergency segment sits immediately in front of the sacrificer's primary cycle package.
        var emergencyPosition = offererPrimary.ReceptionOrderPosition;
        ShiftPositionsFrom(offererSeasonCycles, emergencyPosition);

        var emergencySegment = new SeasonCycle
        {
            CrewId = request.CrewId,
            UserId = request.RequesterUserId,
            SeasonStartDate = offererPrimary.SeasonStartDate,
            CycleCapAtStart = amount,
            UsesSegmentCap = true,
            CapIsProvisional = false,
            EmergencyRequestId = request.Id,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = offererPrimary.PriorityScoreAtSeasonStart,
            ReceptionOrderPosition = emergencyPosition,
            HasCycleStarted = false
        };
        await mutualAidRepository.AddSeasonCycleAsync(emergencySegment, cancellationToken);
        offererSeasonCycles.Add(emergencySegment);

        // Payback sits immediately in front of the requester's primary (same package/season).
        var paybackPosition = requesterPrimary.ReceptionOrderPosition;
        ShiftPositionsFrom(requesterSeasonCycles, paybackPosition);

        var paybackSegment = new SeasonCycle
        {
            CrewId = request.CrewId,
            UserId = offererUserId,
            SeasonStartDate = requesterPrimary.SeasonStartDate,
            CycleCapAtStart = amount,
            UsesSegmentCap = true,
            CapIsProvisional = false,
            EmergencySplitOfferId = splitOffer.Id,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = requesterPrimary.PriorityScoreAtSeasonStart,
            ReceptionOrderPosition = paybackPosition,
            HasCycleStarted = false
        };
        await mutualAidRepository.AddSeasonCycleAsync(paybackSegment, cancellationToken);

        splitOffer.RequesterEmergencyCycle = emergencySegment;
        splitOffer.OffererPaybackCycle = paybackSegment;

        request.AmountSplitCommitted += amount;
        EmergencyRequestAccounting.RefreshFulfilledStatus(request);

        await mutualAidService.RecordEmergencySacrificeAsync(request.CrewId, offererUserId, cancellationToken);
        await mutualAidService.EnsureNextSeasonCyclesAsync(request.CrewId, cancellationToken);

        return EmergencySplitResult.Succeeded("Cycle split recorded.");
    }

    private async Task ResizeSplitOfferAsync(
        EmergencyRequest request,
        EmergencySplitOffer split,
        decimal newAmount,
        CancellationToken cancellationToken)
    {
        if (newAmount < 0m || newAmount > split.Amount)
        {
            return;
        }

        var reduction = split.Amount - newAmount;
        if (reduction <= 0m)
        {
            return;
        }

        var crew = await mutualAidRepository.GetCrewAsync(request.CrewId, cancellationToken);
        if (crew is null || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var offererMembership = await membershipRepository.GetMembershipAsync(
            split.OffererUserId,
            request.CrewId,
            cancellationToken);
        var requesterMembership = await membershipRepository.GetMembershipAsync(
            request.RequesterUserId,
            request.CrewId,
            cancellationToken);
        if (offererMembership is null || requesterMembership is null)
        {
            return;
        }

        var offererIsMember = await mutualAidService.IsFinancialMemberAsync(
            split.OffererUserId,
            request.CrewId,
            offererMembership,
            cancellationToken);
        var requesterIsMember = await mutualAidService.IsFinancialMemberAsync(
            request.RequesterUserId,
            request.CrewId,
            requesterMembership,
            cancellationToken);

        var frozenCapacity = await BuildCapacityContextAsync(crew, useFrozenSeasonCaps: true, cancellationToken);
        var liveCapacity = await BuildCapacityContextAsync(crew, useFrozenSeasonCaps: false, cancellationToken);

        SeasonCycle? emergencySegment = split.RequesterEmergencyCycle;
        SeasonCycle? paybackSegment = split.OffererPaybackCycle;
        if (emergencySegment is null && split.RequesterEmergencyCycleId.HasValue)
        {
            emergencySegment = await mutualAidRepository.GetSeasonCycleByIdAsync(
                split.RequesterEmergencyCycleId.Value,
                cancellationToken);
        }

        if (paybackSegment is null && split.OffererPaybackCycleId.HasValue)
        {
            paybackSegment = await mutualAidRepository.GetSeasonCycleByIdAsync(
                split.OffererPaybackCycleId.Value,
                cancellationToken);
        }

        if (newAmount == 0m)
        {
            CancelLinkedSegment(emergencySegment);
            CancelLinkedSegment(paybackSegment);
            split.IsCancelled = true;
            split.Amount = 0m;
        }
        else
        {
            ResizeLinkedSegment(emergencySegment, newAmount);
            ResizeLinkedSegment(paybackSegment, newAmount);
            split.Amount = newAmount;
        }

        var cyclesBySeason = await LoadCyclesBySeasonAsync(
            request.CrewId,
            crew.CurrentSeasonStartDate.Value,
            cancellationToken);

        var (offererPrimary, _, offererCapacity) = FindPrimaryAcrossSeasons(
            cyclesBySeason,
            split.OffererUserId,
            offererIsMember,
            frozenCapacity,
            liveCapacity);
        var (requesterPrimary, _, requesterCapacity) = FindPrimaryAcrossSeasons(
            cyclesBySeason,
            request.RequesterUserId,
            requesterIsMember,
            frozenCapacity,
            liveCapacity);

        if (offererPrimary is not null && offererCapacity is not null)
        {
            RestoreSegmentCap(offererPrimary, reduction, offererCapacity, offererIsMember);
        }

        if (requesterPrimary is not null && requesterCapacity is not null)
        {
            RestoreSegmentCap(requesterPrimary, reduction, requesterCapacity, requesterIsMember);
        }
    }

    private static void CancelLinkedSegment(SeasonCycle? segment)
    {
        if (segment is null || segment.CycleCompleted)
        {
            return;
        }

        segment.CycleCompleted = true;
        segment.CycleCompletedAt = DateTime.UtcNow;
    }

    private static void ResizeLinkedSegment(SeasonCycle? segment, decimal newCap)
    {
        if (segment is null || segment.CycleCompleted)
        {
            return;
        }

        segment.CycleCapAtStart = Math.Max(segment.CycleReceived, newCap);
        if (segment.CycleReceived >= segment.CycleCapAtStart)
        {
            segment.CycleCompleted = true;
            segment.CycleCompletedAt = DateTime.UtcNow;
        }
    }

    public static string FormatEligibleOffererUserIds(IEnumerable<int> userIds) =>
        string.Join(',', userIds.Distinct().OrderBy(id => id));

    public static IReadOnlyList<int> ParseEligibleUserIds(string? raw)
    {
        if (string.IsNullOrWhiteSpace(raw))
        {
            return Array.Empty<int>();
        }

        return raw.Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(part => int.TryParse(part, out var id) ? id : (int?)null)
            .Where(id => id.HasValue)
            .Select(id => id!.Value)
            .Distinct()
            .ToList();
    }

    private static bool HasEligibilitySnapshot(EmergencyRequest request) =>
        !string.IsNullOrWhiteSpace(request.SplitEligibleOffererUserIds);

    private static bool IsOffererEligibleForRequest(EmergencyRequest request, int offererUserId)
    {
        if (!HasEligibilitySnapshot(request))
        {
            return false;
        }

        return ParseEligibleUserIds(request.SplitEligibleOffererUserIds).Contains(offererUserId);
    }

    private async Task<decimal> GetOffererRemainingAsync(
        int crewId,
        int offererUserId,
        CancellationToken cancellationToken)
    {
        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null || !crew.CurrentSeasonStartDate.HasValue)
        {
            return 0m;
        }

        var membership = await membershipRepository.GetMembershipAsync(offererUserId, crewId, cancellationToken);
        if (membership is null)
        {
            return 0m;
        }

        var cyclesBySeason = await LoadCyclesBySeasonAsync(crewId, crew.CurrentSeasonStartDate.Value, cancellationToken);
        var frozenCapacity = await BuildCapacityContextAsync(crew, useFrozenSeasonCaps: true, cancellationToken);
        var liveCapacity = await BuildCapacityContextAsync(crew, useFrozenSeasonCaps: false, cancellationToken);
        var isMember = await mutualAidService.IsFinancialMemberAsync(offererUserId, crewId, membership, cancellationToken);
        var (primary, _, capacity) = FindPrimaryAcrossSeasons(
            cyclesBySeason,
            offererUserId,
            isMember,
            frozenCapacity,
            liveCapacity);
        if (primary is null || capacity is null)
        {
            return 0m;
        }

        return GetSegmentRemaining(primary, capacity, isMember);
    }

    private async Task<Dictionary<DateTime, List<SeasonCycle>>> LoadCyclesBySeasonAsync(
        int crewId,
        DateTime currentSeasonStart,
        CancellationToken cancellationToken)
    {
        var seasonDates = (await mutualAidRepository.GetSeasonStartDatesOnOrAfterAsync(
            crewId,
            currentSeasonStart,
            cancellationToken)).ToList();
        if (!seasonDates.Contains(currentSeasonStart))
        {
            seasonDates.Insert(0, currentSeasonStart);
        }

        var cyclesBySeason = new Dictionary<DateTime, List<SeasonCycle>>();
        foreach (var seasonStart in seasonDates.Distinct().OrderBy(d => d))
        {
            cyclesBySeason[seasonStart] = (await mutualAidRepository.GetSeasonCyclesAsync(
                crewId,
                seasonStart,
                cancellationToken)).ToList();
        }

        return cyclesBySeason;
    }

    public static decimal ResolveSegmentCap(
        SeasonCycle cycle,
        bool isFinancialMember,
        decimal memberCycleCap,
        decimal nonMemberCycleCap)
    {
        if (cycle.UsesSegmentCap || cycle.EmergencyRequestId.HasValue || cycle.EmergencySplitOfferId.HasValue)
        {
            return cycle.CycleCapAtStart;
        }

        var live = isFinancialMember ? memberCycleCap : nonMemberCycleCap;
        if (cycle.CapIsProvisional)
        {
            return Math.Max(0m, live - cycle.SplitReservedAmount);
        }

        return live;
    }

    private static (SeasonCycle? Primary, List<SeasonCycle>? SeasonCycles, EmergencyCapacityContext? Capacity)
        FindPrimaryAcrossSeasons(
            IReadOnlyDictionary<DateTime, List<SeasonCycle>> cyclesBySeason,
            int userId,
            bool isFinancialMember,
            EmergencyCapacityContext frozenCapacity,
            EmergencyCapacityContext liveCapacity)
    {
        foreach (var (_, cycles) in cyclesBySeason.OrderBy(kv => kv.Key))
        {
            var sample = cycles.FirstOrDefault(c =>
                c.UserId == userId
                && !c.CycleCompleted
                && c.EmergencyRequestId is null
                && c.EmergencySplitOfferId is null);
            var capacity = sample?.CapIsProvisional == true ? liveCapacity : frozenCapacity;
            var primary = FindPrimarySegment(cycles, userId, capacity, isFinancialMember);
            if (primary is not null)
            {
                return (primary, cycles, capacity);
            }
        }

        return (null, null, null);
    }

    private static SeasonCycle? FindPrimarySegment(
        IReadOnlyList<SeasonCycle> cycles,
        int userId,
        EmergencyCapacityContext capacityContext,
        bool isFinancialMember) =>
        cycles
            .Where(c => c.UserId == userId
                && !c.CycleCompleted
                && c.EmergencyRequestId is null
                && c.EmergencySplitOfferId is null)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault(c => GetSegmentRemaining(c, capacityContext, isFinancialMember) > 0);

    private static decimal GetSegmentRemaining(
        SeasonCycle cycle,
        EmergencyCapacityContext capacityContext,
        bool isFinancialMember)
    {
        var cap = ResolveSegmentCap(
            cycle,
            isFinancialMember,
            capacityContext.MemberCycleCap,
            capacityContext.NonMemberCycleCap);
        return Math.Max(0m, cap - cycle.CycleReceived);
    }

    private static void ReduceSegmentCap(
        SeasonCycle segment,
        decimal amount,
        EmergencyCapacityContext capacityContext,
        bool isFinancialMember)
    {
        if (segment.CapIsProvisional && !segment.UsesSegmentCap)
        {
            segment.SplitReservedAmount += amount;
            var remaining = GetSegmentRemaining(segment, capacityContext, isFinancialMember);
            if (remaining <= 0m)
            {
                segment.CycleCompleted = true;
                segment.CycleCompletedAt = DateTime.UtcNow;
            }

            return;
        }

        var currentCap = ResolveSegmentCap(
            segment,
            isFinancialMember,
            capacityContext.MemberCycleCap,
            capacityContext.NonMemberCycleCap);
        segment.UsesSegmentCap = true;
        segment.CycleCapAtStart = Math.Max(0m, currentCap - amount);
        if (segment.CycleCapAtStart <= segment.CycleReceived)
        {
            segment.CycleCompleted = true;
            segment.CycleCompletedAt = DateTime.UtcNow;
        }
    }

    public static void RestoreSegmentCap(
        SeasonCycle segment,
        decimal amount,
        EmergencyCapacityContext capacityContext,
        bool isFinancialMember)
    {
        if (amount <= 0m)
        {
            return;
        }

        if (segment.CapIsProvisional && !segment.UsesSegmentCap)
        {
            segment.SplitReservedAmount = Math.Max(0m, segment.SplitReservedAmount - amount);
            if (segment.CycleCompleted && GetSegmentRemaining(segment, capacityContext, isFinancialMember) > 0m)
            {
                segment.CycleCompleted = false;
                segment.CycleCompletedAt = null;
            }

            return;
        }

        if (segment.UsesSegmentCap)
        {
            segment.CycleCapAtStart += amount;
            if (segment.CycleCompleted && segment.CycleReceived < segment.CycleCapAtStart)
            {
                segment.CycleCompleted = false;
                segment.CycleCompletedAt = null;
            }

            return;
        }

        var currentCap = ResolveSegmentCap(
            segment,
            isFinancialMember,
            capacityContext.MemberCycleCap,
            capacityContext.NonMemberCycleCap);
        segment.UsesSegmentCap = true;
        segment.CycleCapAtStart = currentCap + amount;
        if (segment.CycleCompleted && segment.CycleReceived < segment.CycleCapAtStart)
        {
            segment.CycleCompleted = false;
            segment.CycleCompletedAt = null;
        }
    }

    private static void ShiftPositionsFrom(IList<SeasonCycle> cycles, int fromPosition)
    {
        foreach (var cycle in cycles.Where(c => c.ReceptionOrderPosition >= fromPosition))
        {
            cycle.ReceptionOrderPosition++;
        }
    }

    private async Task<EmergencyCapacityContext> BuildCapacityContextAsync(
        Crew crew,
        bool useFrozenSeasonCaps,
        CancellationToken cancellationToken)
    {
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var totalMonthly = await mutualAidService.GetCrewMonthlyGivingCapacityAsync(crew.Id, cancellationToken);
        var thresholdRecipients = participants.Count(p => p.User.NeedsSurvivalAid);
        var survivalAmount = MutualAidCalculationService.GetSurvivalThresholdAmount(totalMonthly, thresholdRecipients);

        var liveMemberCap = MutualAidCalculationService.GetMemberCycleCap(crew, totalMonthly);
        var liveNonMemberCap = MutualAidCalculationService.GetNonMemberCycleCap(crew, totalMonthly);

        if (!useFrozenSeasonCaps)
        {
            return new EmergencyCapacityContext
            {
                MemberCycleCap = liveMemberCap,
                NonMemberCycleCap = liveNonMemberCap,
                SurvivalThresholdAmount = survivalAmount
            };
        }

        return new EmergencyCapacityContext
        {
            MemberCycleCap = ResolveEffectiveCap(crew.SeasonMemberCycleCap, liveMemberCap),
            NonMemberCycleCap = ResolveEffectiveCap(crew.SeasonNonMemberCycleCap, liveNonMemberCap),
            SurvivalThresholdAmount = survivalAmount
        };
    }

    private static decimal ResolveEffectiveCap(decimal seasonStartCap, decimal liveCap)
    {
        if (seasonStartCap <= 0m)
        {
            return liveCap;
        }

        return MutualAidCalculationService.GetEffectiveMemberCycleCap(seasonStartCap, liveCap);
    }
}

public sealed class EmergencyCapacityContext
{
    public decimal MemberCycleCap { get; init; }
    public decimal NonMemberCycleCap { get; init; }
    public decimal SurvivalThresholdAmount { get; init; }
}
