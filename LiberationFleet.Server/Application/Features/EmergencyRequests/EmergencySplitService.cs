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

public class EmergencySplitService(
    IMutualAidRepository mutualAidRepository,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IMutualAidService mutualAidService)
{
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

        var remaining = request.AmountNeeded - request.AmountFulfilled;
        if (amount > remaining)
        {
            return EmergencySplitResult.Failed("Split amount exceeds the remaining emergency need.");
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

        var cycles = (await mutualAidRepository.GetSeasonCyclesAsync(
            request.CrewId,
            crew.CurrentSeasonStartDate.Value,
            cancellationToken)).ToList();

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
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

        var offererPrimary = FindPrimarySegment(cycles, offererUserId, capacityContext, offererIsMember);
        if (offererPrimary is null)
        {
            return EmergencySplitResult.Failed("You do not have an active cycle segment to split from.");
        }

        var offererRemaining = GetSegmentRemaining(
            offererPrimary,
            capacityContext,
            offererIsMember);
        if (amount > offererRemaining)
        {
            return EmergencySplitResult.Failed($"You can split at most ${offererRemaining:0.##} from your cycle.");
        }

        var requesterPrimary = FindPrimarySegment(cycles, request.RequesterUserId, capacityContext, requesterIsMember);
        if (requesterPrimary is null)
        {
            return EmergencySplitResult.Failed("The requester does not have an active cycle segment.");
        }

        var requesterRemaining = GetSegmentRemaining(
            requesterPrimary,
            capacityContext,
            requesterIsMember);
        if (amount > requesterRemaining)
        {
            return EmergencySplitResult.Failed("The requester does not have enough cycle capacity for this split.");
        }

        var splitOffer = new EmergencySplitOffer
        {
            EmergencyRequest = request,
            OffererUserId = offererUserId,
            Amount = amount,
            CreatedAt = DateTime.UtcNow
        };
        await emergencyRequestRepository.AddSplitOfferAsync(splitOffer, cancellationToken);

        ReduceSegmentCap(offererPrimary, amount, capacityContext, offererIsMember);
        ReduceSegmentCap(requesterPrimary, amount, capacityContext, requesterIsMember);

        var minPosition = cycles
            .Where(c => !c.CycleCompleted)
            .Select(c => (int?)c.ReceptionOrderPosition)
            .DefaultIfEmpty(0)
            .Min() ?? 0;

        var emergencySegment = new SeasonCycle
        {
            CrewId = request.CrewId,
            UserId = request.RequesterUserId,
            SeasonStartDate = crew.CurrentSeasonStartDate.Value,
            CycleCapAtStart = amount,
            UsesSegmentCap = true,
            EmergencyRequestId = request.Id,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = requesterPrimary.PriorityScoreAtSeasonStart,
            ReceptionOrderPosition = minPosition,
            HasCycleStarted = false
        };
        await mutualAidRepository.AddSeasonCycleAsync(emergencySegment, cancellationToken);
        cycles.Add(emergencySegment);

        var paybackPosition = requesterPrimary.ReceptionOrderPosition;
        ShiftPositionsFrom(cycles, paybackPosition);

        var paybackSegment = new SeasonCycle
        {
            CrewId = request.CrewId,
            UserId = offererUserId,
            SeasonStartDate = crew.CurrentSeasonStartDate.Value,
            CycleCapAtStart = amount,
            UsesSegmentCap = true,
            EmergencySplitOfferId = splitOffer.Id,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = offererPrimary.PriorityScoreAtSeasonStart,
            ReceptionOrderPosition = paybackPosition,
            HasCycleStarted = false
        };
        await mutualAidRepository.AddSeasonCycleAsync(paybackSegment, cancellationToken);

        request.AmountFulfilled += amount;
        if (request.AmountFulfilled >= request.AmountNeeded)
        {
            request.Status = EmergencyRequestStatus.Fulfilled;
        }

        return EmergencySplitResult.Succeeded("Cycle split recorded.");
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

        return isFinancialMember ? memberCycleCap : nonMemberCycleCap;
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

    private static void ShiftPositionsFrom(IList<SeasonCycle> cycles, int fromPosition)
    {
        foreach (var cycle in cycles.Where(c => c.ReceptionOrderPosition >= fromPosition))
        {
            cycle.ReceptionOrderPosition++;
        }
    }

    private async Task<EmergencyCapacityContext> BuildCapacityContextAsync(Crew crew, CancellationToken cancellationToken)
    {
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var totalContributions = participants
            .Where(p => p.EstimatedMonthlyContribution.HasValue)
            .Select(p => p.EstimatedMonthlyContribution!.Value);
        var totalMonthly = MutualAidCalculationService.GetTotalMonthlyContributions(totalContributions);
        var thresholdRecipients = participants.Count(p => p.User.NeedsSurvivalAid);
        var survivalAmount = MutualAidCalculationService.GetSurvivalThresholdAmount(totalMonthly, thresholdRecipients);

        return new EmergencyCapacityContext
        {
            MemberCycleCap = MutualAidCalculationService.GetMemberCycleCap(crew, totalMonthly),
            NonMemberCycleCap = MutualAidCalculationService.GetNonMemberCycleCap(crew, totalMonthly),
            SurvivalThresholdAmount = survivalAmount
        };
    }
}

public sealed class EmergencyCapacityContext
{
    public decimal MemberCycleCap { get; init; }
    public decimal NonMemberCycleCap { get; init; }
    public decimal SurvivalThresholdAmount { get; init; }
}
