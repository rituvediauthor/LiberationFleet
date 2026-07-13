using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Services;

public partial class MutualAidService(
    IMutualAidRepository mutualAidRepository,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IMutualAidService, IMutualAidDevService
{
    public async Task<SeasonStatusDto> GetSeasonStatusAsync(int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new SeasonStatusDto();
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new SeasonStatusDto();
        }

        await TryStartSeasonIfReadyAsync(crew, cancellationToken);

        membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken) ?? membership;
        crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken) ?? crew;

        var readyMembers = await mutualAidRepository.GetSeasonReadyMembersAsync(crew.Id, cancellationToken);

        return new SeasonStatusDto
        {
            SeasonStarted = crew.SeasonStarted,
            UserInSeason = membership.IsInSeason,
            UserSeasonReady = membership.IsSeasonReady,
            ReadyCount = readyMembers.Count,
            CanStartSeason = !crew.SeasonStarted && readyMembers.Count >= 3,
            EstimatedMonthlyContribution = membership.EstimatedMonthlyContribution
        };
    }

    public async Task<IReadOnlyList<ReceptionOrderEntryDto>> GetReceptionOrderAsync(
        int userId,
        int limit = 30,
        bool requireGiverInSeason = true,
        bool excludeSelfAsRecipient = true,
        bool forRecordGift = false,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        if (requireGiverInSeason && !membership.IsInSeason)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        var entries = await BuildReceptionOrderForCrewAsync(
            crew,
            userId,
            excludeSelfAsRecipient,
            forRecordGift,
            additionalMembersForMiddlemen: null,
            cancellationToken);

        return entries.Take(limit).ToList();
    }

    public async Task<IReadOnlyList<ReceptionOrderEntryDto>> GetReceptionOrderForCrewAsGiverAsync(
        int targetCrewId,
        int giverUserId,
        bool forRecordGift = false,
        bool excludeSelfAsRecipient = true,
        IReadOnlyList<CrewMemberPlatforms>? additionalMembersForMiddlemen = null,
        CancellationToken cancellationToken = default)
    {
        var crew = await mutualAidRepository.GetCrewAsync(targetCrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        return await BuildReceptionOrderForCrewAsync(
            crew,
            giverUserId,
            excludeSelfAsRecipient,
            forRecordGift,
            additionalMembersForMiddlemen,
            cancellationToken);
    }

    private async Task<IReadOnlyList<ReceptionOrderEntryDto>> BuildReceptionOrderForCrewAsync(
        Crew crew,
        int giverUserId,
        bool excludeSelfAsRecipient,
        bool forRecordGift,
        IReadOnlyList<CrewMemberPlatforms>? additionalMembersForMiddlemen,
        CancellationToken cancellationToken)
    {
        await TryCreateFirstOfMonthThresholdsAsync(crew, cancellationToken);

        var allMembers = await mutualAidRepository.GetActiveMembersWithUsersAsync(crew.Id, cancellationToken);
        var memberPlatforms = allMembers.Select(CrewPaymentPlatformService.MapCrewMemberPlatforms).ToList();

        var middlemanPool = BuildMiddlemanPool(memberPlatforms, additionalMembersForMiddlemen);
        var giverPlatforms = await ResolveGiverPlatformIdsAsync(
            giverUserId,
            memberPlatforms,
            middlemanPool,
            cancellationToken);

        // FindMiddlemen requires the giver in the members list.
        if (!middlemanPool.Any(m => m.UserId == giverUserId))
        {
            var giverMember = await ResolveGiverMemberPlatformsAsync(giverUserId, cancellationToken);
            if (giverMember is not null)
            {
                middlemanPool.Add(giverMember);
            }
        }

        var entries = new List<ReceptionOrderEntryDto>();

        if (AreSurvivalThresholdsEnabled(crew))
        {
            var thresholds = await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crew.Id, cancellationToken);
            foreach (var threshold in thresholds)
            {
                var need = threshold.ThresholdAmount - threshold.ReceivedAmount;
                if (need <= 0)
                {
                    continue;
                }

                entries.Add(BuildEntry(
                    threshold.UserId,
                    threshold.User.Username,
                    need,
                    "survivalThreshold",
                    threshold.Id,
                    null,
                    null,
                    giverUserId,
                    giverPlatforms,
                    middlemanPool));
            }
        }

        var cycles = (await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate!.Value, cancellationToken)).ToList();
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var seasonParticipants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var memberStatus = new Dictionary<int, bool>();
        foreach (var participant in seasonParticipants)
        {
            memberStatus[participant.UserId] = await IsFinancialMemberAsync(participant.UserId, crew.Id, participant, cancellationToken);
        }

        var effectiveMemberCap = GetEffectiveCycleCap(true, crew, capacityContext);
        var effectiveNonMemberCap = GetEffectiveCycleCap(false, crew, capacityContext);

        decimal CapFor(SeasonCycle cycle)
        {
            var isMember = memberStatus.GetValueOrDefault(cycle.UserId, false);
            return EmergencySplitService.ResolveSegmentCap(
                cycle,
                isMember,
                effectiveMemberCap,
                effectiveNonMemberCap);
        }

        bool UserNeedsAid(SeasonCycle cycle) =>
            cycle.User?.InNeedOfAid != false;

        // Incomplete for order/locking is CycleCompleted=false. Catch-up stays on
        // completed cycles via GetCatchUpAmount (virtual entries), not this list.
        var allIncompleteCycles = cycles
            .Where(c => !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToList();

        await RefreshHasCycleStartedAsync(crew, allIncompleteCycles, cancellationToken);

        var incompleteCycles = allIncompleteCycles
            .Where(UserNeedsAid)
            .ToList();

        var units = BuildIncompleteUnits(incompleteCycles);
        var (leader, runnerUp) = FindLockedLeaderAndRunnerUp(incompleteCycles);
        var lockedUnits = new List<List<SeasonCycle>>();
        var lockedCycleIds = new HashSet<int>();

        void AddLockedUnit(SeasonCycle? anchor)
        {
            if (anchor is null)
            {
                return;
            }

            var unit = units.FirstOrDefault(u => u.Any(c => c.Id == anchor.Id));
            if (unit is null || unit.Any(c => lockedCycleIds.Contains(c.Id)))
            {
                return;
            }

            lockedUnits.Add(unit);
            foreach (var cycle in unit)
            {
                lockedCycleIds.Add(cycle.Id);
            }
        }

        AddLockedUnit(leader);
        AddLockedUnit(runnerUp);

        void AddCycleEntry(SeasonCycle cycle, string entryType, decimal need)
        {
            if (need <= 0)
            {
                return;
            }

            var username = cycle.User?.Username
                ?? allMembers.FirstOrDefault(m => m.UserId == cycle.UserId)?.User.Username
                ?? string.Empty;

            entries.Add(BuildEntry(
                cycle.UserId,
                username,
                need,
                entryType,
                null,
                cycle.UserId,
                cycle.Id,
                giverUserId,
                giverPlatforms,
                middlemanPool));
        }

        foreach (var unit in lockedUnits)
        {
            foreach (var cycle in unit.OrderBy(c => c.ReceptionOrderPosition))
            {
                AddCycleEntry(cycle, "cycle", CapFor(cycle) - cycle.CycleReceived);
            }
        }

        if (!forRecordGift)
        {
            foreach (var cycle in cycles
                .Where(c => c.CycleCompleted && UserNeedsAid(c) && !c.UsesSegmentCap)
                .OrderBy(c => c.ReceptionOrderPosition))
            {
                var catchUp = MutualAidCalculationService.GetCatchUpAmount(cycle, CapFor(cycle));
                if (catchUp <= 0)
                {
                    continue;
                }

                AddCycleEntry(cycle, "catchUp", catchUp);
            }

            foreach (var unit in units)
            {
                if (unit.Any(c => lockedCycleIds.Contains(c.Id)))
                {
                    continue;
                }

                foreach (var cycle in unit.OrderBy(c => c.ReceptionOrderPosition))
                {
                    AddCycleEntry(cycle, "cycle", CapFor(cycle) - cycle.CycleReceived);
                }
            }
        }

        var result = entries.AsEnumerable();
        if (excludeSelfAsRecipient)
        {
            result = result.Where(e => e.UserId != giverUserId);
        }

        return result.ToList();
    }

    private static List<CrewMemberPlatforms> BuildMiddlemanPool(
        IReadOnlyList<CrewMemberPlatforms> targetCrewMembers,
        IReadOnlyList<CrewMemberPlatforms>? additionalMembersForMiddlemen)
    {
        var pool = new List<CrewMemberPlatforms>(targetCrewMembers);
        if (additionalMembersForMiddlemen is null || additionalMembersForMiddlemen.Count == 0)
        {
            return pool;
        }

        var seen = pool.Select(m => m.UserId).ToHashSet();
        foreach (var member in additionalMembersForMiddlemen)
        {
            if (seen.Add(member.UserId))
            {
                pool.Add(member);
            }
        }

        return pool;
    }

    private async Task<IReadOnlyList<int>> ResolveGiverPlatformIdsAsync(
        int giverUserId,
        IReadOnlyList<CrewMemberPlatforms> targetCrewMembers,
        IReadOnlyList<CrewMemberPlatforms> middlemanPool,
        CancellationToken cancellationToken)
    {
        var fromTarget = targetCrewMembers.FirstOrDefault(m => m.UserId == giverUserId);
        if (fromTarget is not null)
        {
            return fromTarget.PlatformIds;
        }

        var fromPool = middlemanPool.FirstOrDefault(m => m.UserId == giverUserId);
        if (fromPool is not null)
        {
            return fromPool.PlatformIds;
        }

        var giverMember = await ResolveGiverMemberPlatformsAsync(giverUserId, cancellationToken);
        return giverMember?.PlatformIds ?? Array.Empty<int>();
    }

    private async Task<CrewMemberPlatforms?> ResolveGiverMemberPlatformsAsync(
        int giverUserId,
        CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(giverUserId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var giverCrewMembers = await mutualAidRepository.GetActiveMembersWithUsersAsync(
            membership.CrewId,
            cancellationToken);
        var giverMembership = giverCrewMembers.FirstOrDefault(m => m.UserId == giverUserId);
        return giverMembership is null
            ? null
            : CrewPaymentPlatformService.MapCrewMemberPlatforms(giverMembership);
    }

    public async Task<NextAidDto?> GetNextAidAsync(int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted)
        {
            return null;
        }

        var entries = await GetReceptionOrderAsync(
            userId,
            1,
            requireGiverInSeason: false,
            excludeSelfAsRecipient: false,
            forRecordGift: false,
            cancellationToken);
        var first = entries.FirstOrDefault();
        if (first is null)
        {
            return null;
        }

        var platformDisplay = ResolveNextAidPlatformDisplay(first, userId);

        return new NextAidDto
        {
            RecipientName = first.Username,
            Amount = first.AmountNeeded,
            IsCurrentUserRecipient = first.UserId == userId,
            PlatformDisplayKind = platformDisplay.Kind,
            PlatformName = platformDisplay.Name,
            PlatformHandle = platformDisplay.Handle
        };
    }

    private static (string Kind, string? Name, string? Handle) ResolveNextAidPlatformDisplay(
        ReceptionOrderEntryDto entry,
        int viewerUserId)
    {
        if (entry.UserId == viewerUserId)
        {
            return (NextAidPlatformDisplayKind.None, null, null);
        }

        if (entry.CommonPlatformIds.Count > 0)
        {
            if (!string.IsNullOrEmpty(entry.RecipientPreferredPlatformName))
            {
                var preferred = entry.RecipientPlatformAccounts
                    .FirstOrDefault(a => a.Name == entry.RecipientPreferredPlatformName);
                if (preferred is not null)
                {
                    return (NextAidPlatformDisplayKind.Preferred, preferred.Name, preferred.Handle);
                }
            }

            var common = entry.RecipientPlatformAccounts.FirstOrDefault();
            if (common is not null)
            {
                return (NextAidPlatformDisplayKind.Common, common.Name, common.Handle);
            }
        }

        if (entry.MiddlemanOptions.Count > 0)
        {
            return (NextAidPlatformDisplayKind.MiddlemanNeeded, null, null);
        }

        return (NextAidPlatformDisplayKind.Unavailable, null, null);
    }

    public async Task<SeasonReadyResultDto> MarkSeasonReadyAsync(
        int userId,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new SeasonReadyResultDto { Success = false, Message = "You are not in a crew." };
        }

        if (membership.EstimatedMonthlyContribution <= 0)
        {
            return new SeasonReadyResultDto { Success = false, Message = "Save your season setup before marking ready." };
        }

        var members = await mutualAidRepository.GetActiveMembersWithUsersAsync(membership.CrewId, cancellationToken);
        var current = members.First(m => m.UserId == userId);
        if (current.User.PaymentPlatforms.Count == 0)
        {
            return new SeasonReadyResultDto { Success = false, Message = "Register at least one payment platform before marking ready." };
        }

        membership.IsSeasonReady = true;

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new SeasonReadyResultDto { Success = false, Message = "Crew not found." };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var seasonStarted = false;
        if (!crew.SeasonStarted)
        {
            seasonStarted = await TryStartSeasonIfReadyAsync(crew, cancellationToken);
        }
        else if (!membership.IsInSeason)
        {
            await JoinActiveSeasonAsync(crew, membership, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var status = await GetSeasonStatusAsync(userId, cancellationToken);
        return new SeasonReadyResultDto
        {
            Success = true,
            Message = seasonStarted ? "Season started." : membership.IsInSeason ? "Joined season." : "Marked ready.",
            SeasonStarted = seasonStarted || crew.SeasonStarted,
            Status = status
        };
    }

    public async Task<SeasonSetupSaveResultDto> SaveSeasonSetupAsync(
        int userId,
        decimal estimatedMonthlyContribution,
        CancellationToken cancellationToken = default)
    {
        if (estimatedMonthlyContribution <= 0)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "Estimated monthly contribution must be greater than zero." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "You are not in a crew." };
        }

        membership.EstimatedMonthlyContribution = estimatedMonthlyContribution;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var status = await GetSeasonStatusAsync(userId, cancellationToken);
        return new SeasonSetupSaveResultDto
        {
            Success = true,
            Message = "Season setup saved.",
            Status = status
        };
    }

    public async Task<SeasonSetupSaveResultDto> ClearSeasonReadyAsync(int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "You are not in a crew." };
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "Crew not found." };
        }

        if (crew.SeasonStarted)
        {
            return new SeasonSetupSaveResultDto { Success = false, Message = "Cannot change ready status after the season has started." };
        }

        membership.IsSeasonReady = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var status = await GetSeasonStatusAsync(userId, cancellationToken);
        return new SeasonSetupSaveResultDto
        {
            Success = true,
            Message = "Ready status cleared.",
            Status = status
        };
    }

    public async Task ApplyGiftReceptionAsync(Gift gift, CancellationToken cancellationToken = default)
    {
        if (gift.ReceptionApplied)
        {
            return;
        }

        await ApplyGiftReceptionForUserAsync(gift, gift.RecipientUserId, cancellationToken);
        gift.ReceptionApplied = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task ApplyGiftReceptionForUserAsync(
        Gift gift,
        int recipientUserId,
        CancellationToken cancellationToken = default)
    {
        if (!gift.CountsTowardReception)
        {
            return;
        }

        var crew = await mutualAidRepository.GetCrewAsync(gift.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        // Emergency gifts are logged as custom but must credit the emergency segment, not the waterfall.
        if (gift.IsCustomGift && !gift.EmergencyRequestId.HasValue)
        {
            await ApplyCustomGiftWaterfallAsync(gift, recipientUserId, crew, cancellationToken);
            await RefreshHasCycleStartedForCrewAsync(crew, cancellationToken);
            await TryEndSeasonAsync(crew, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return;
        }

        var cycle = gift.SeasonCycleId.HasValue
            ? await mutualAidRepository.GetSeasonCycleByIdAsync(gift.SeasonCycleId.Value, cancellationToken)
            : null;

        if (cycle is null && gift.EmergencyRequestId.HasValue)
        {
            var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
                gift.CrewId,
                crew.CurrentSeasonStartDate.Value,
                cancellationToken);
            cycle = cycles
                .Where(c => c.EmergencyRequestId == gift.EmergencyRequestId && !c.CycleCompleted)
                .OrderBy(c => c.ReceptionOrderPosition)
                .FirstOrDefault();
        }

        if (cycle is null)
        {
            cycle = await FindPrimaryCycleForUserAsync(
                gift.CrewId,
                recipientUserId,
                crew.CurrentSeasonStartDate.Value,
                cancellationToken);
        }

        if (cycle is null)
        {
            return;
        }

        cycle.TotalReceptionAmount += gift.Amount;

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var membership = await membershipRepository.GetMembershipAsync(recipientUserId, gift.CrewId, cancellationToken);
        var isFinancialMember = membership is not null
            && await IsFinancialMemberAsync(recipientUserId, gift.CrewId, membership, cancellationToken);
        var effectiveMemberCap = GetEffectiveCycleCap(true, crew, capacityContext);
        var effectiveNonMemberCap = GetEffectiveCycleCap(false, crew, capacityContext);
        var effectiveCap = EmergencySplitService.ResolveSegmentCap(
            cycle,
            isFinancialMember,
            effectiveMemberCap,
            effectiveNonMemberCap);

        if (gift.IsSurvivalThreshold && crew.AllowSurvivalThresholds)
        {
            cycle.SurvivalThresholdReceived += gift.Amount;
            await ApplyToThresholdForUserAsync(gift, recipientUserId, cancellationToken);
        }
        else if (cycle.CycleCompleted)
        {
            // Catch-up: grow CycleReceived while keeping CycleCompleted true.
            var room = Math.Max(0m, effectiveCap - cycle.CycleReceived);
            if (room > 0)
            {
                cycle.CycleReceived += Math.Min(gift.Amount, room);
                if (cycle.CycleReceived >= effectiveCap)
                {
                    cycle.CycleCapAtCompletion = effectiveCap;
                }
            }
        }
        else
        {
            // Do not set HasCycleStarted here; RefreshHasCycleStartedForCrewAsync owns that.
            var cycleRoom = effectiveCap - cycle.CycleReceived;
            if (cycleRoom > 0)
            {
                cycle.CycleReceived += Math.Min(gift.Amount, cycleRoom);
            }

            UpdateCycleCompletion(cycle, effectiveCap);
        }

        await RefreshHasCycleStartedForCrewAsync(crew, cancellationToken);
        await TryEndSeasonAsync(crew, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task OnCrewmatePriorityChangedAsync(int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return;
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var newScore = await GetPriorityScoreForUserAsync(
            userId,
            crew.Id,
            cancellationToken,
            excludeActiveSeasonContributions: true);
        membership.CurrentPriorityScore = newScore;

        await RepositionCrewmateInReceptionOrderAsync(crew, userId, newScore, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecalculateCapsAfterMembershipChangeAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        // Do not overwrite SeasonMemberCycleCap / SeasonNonMemberCycleCap — those stay frozen.
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate.Value, cancellationToken);

        foreach (var cycle in cycles)
        {
            decimal cap;
            if (cycle.UsesSegmentCap || !IsPrimaryCycle(cycle))
            {
                cap = cycle.CycleCapAtStart;
            }
            else
            {
                cap = await GetEffectiveCycleCapForUserAsync(
                    cycle.UserId,
                    crew,
                    capacityContext,
                    cancellationToken);
            }

            UpdateCycleCompletion(cycle, cap);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RemoveMemberFromSeasonAsync(int crewId, int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetMembershipAsync(userId, crewId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        membership.IsInSeason = false;
        membership.IsSeasonReady = false;

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew?.SeasonStarted == true && crew.CurrentSeasonStartDate.HasValue)
        {
            var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
                crewId,
                crew.CurrentSeasonStartDate.Value,
                cancellationToken);
            foreach (var cycle in cycles.Where(c => c.UserId == userId && !c.CycleCompleted))
            {
                cycle.CycleCompleted = true;
                cycle.CycleCompletedAt ??= DateTime.UtcNow;
            }

            await RefreshHasCycleStartedForCrewAsync(crew, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordEmergencySacrificeAsync(
        int crewId,
        int sacrificerUserId,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetMembershipAsync(sacrificerUserId, crewId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        membership.EmergencySacrificesThisSeason++;
        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task RecordIntermediaryFailureAsync(
        int crewId,
        int intermediaryUserId,
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetMembershipAsync(intermediaryUserId, crewId, cancellationToken);
        if (membership is null)
        {
            return;
        }

        membership.IntermediaryFailedCompletions++;
        if (membership.IntermediaryFailedCompletions >= 2)
        {
            membership.IsIntermediary = false;
            membership.IntermediaryFailedCompletions = 0;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    public async Task<decimal> GetPriorityScoreForUserAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default,
        bool excludeActiveSeasonContributions = false)
    {
        var members = await mutualAidRepository.GetActiveMembersWithUsersAsync(crewId, cancellationToken);
        var membership = members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
        {
            return 0m;
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        DateTime? contributionCutoff = null;
        if (excludeActiveSeasonContributions
            && crew?.SeasonStarted == true
            && crew.CurrentSeasonStartDate.HasValue)
        {
            contributionCutoff = crew.CurrentSeasonStartDate.Value;
        }

        var crewLifetime = await mutualAidRepository.GetCrewLifetimeContributionsAsync(
            crewId,
            contributionCutoff,
            cancellationToken);
        var userLifetime = await mutualAidRepository.GetLifetimeContributionsAsync(
            userId,
            crewId,
            contributionCutoff,
            cancellationToken);
        var capacityContext = crew is not null
            ? await BuildCapacityContextAsync(crew, cancellationToken)
            : new CapacityContext();

        return MutualAidCalculationService.CalculatePriorityScore(
            membership.User,
            membership,
            await IsFinancialMemberAsync(
                userId,
                crewId,
                membership,
                cancellationToken,
                excludeActiveSeasonContributions),
            crewLifetime,
            userLifetime,
            capacityContext.SurvivalThresholdAmount);
    }

    public async Task<bool> IsFinancialMemberAsync(
        int userId,
        int crewId,
        CrewMembership membership,
        CancellationToken cancellationToken = default,
        bool excludeActiveSeasonContributions = false)
    {
        if (membership.IsHonoraryMember || CrewRoleMapper.HasAnyRole(membership))
        {
            return true;
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return false;
        }

        if (!excludeActiveSeasonContributions
            && await mutualAidRepository.HasContributedSinceAsync(userId, crewId, crew.CurrentSeasonStartDate.Value, cancellationToken: cancellationToken))
        {
            return true;
        }

        var previousStart = await mutualAidRepository.GetPreviousSeasonStartDateAsync(crewId, crew.CurrentSeasonStartDate.Value, cancellationToken);
        if (previousStart.HasValue)
        {
            return await mutualAidRepository.HasContributedSinceAsync(
                userId,
                crewId,
                previousStart.Value,
                crew.CurrentSeasonStartDate.Value,
                cancellationToken);
        }

        return false;
    }

    public IReadOnlyList<int> FindMiddlemen(int giverUserId, int recipientUserId, IReadOnlyList<CrewMemberPlatforms> members)
    {
        var giver = members.FirstOrDefault(m => m.UserId == giverUserId);
        var recipient = members.FirstOrDefault(m => m.UserId == recipientUserId);
        if (giver is null || recipient is null)
        {
            return Array.Empty<int>();
        }

        if (giver.PlatformIds.Intersect(recipient.PlatformIds).Any())
        {
            return Array.Empty<int>();
        }

        return members
            .Where(m => m.IsIntermediary
                && m.UserId != giverUserId
                && m.UserId != recipientUserId
                && m.PlatformIds.Intersect(giver.PlatformIds).Any()
                && m.PlatformIds.Intersect(recipient.PlatformIds).Any())
            .Select(m => m.UserId)
            .ToList();
    }

    private ReceptionOrderEntryDto BuildEntry(
        int recipientUserId,
        string username,
        decimal need,
        string entryType,
        int? thresholdId,
        int? cycleUserId,
        int? seasonCycleId,
        int giverUserId,
        IReadOnlyList<int> giverPlatformIds,
        IReadOnlyList<CrewMemberPlatforms> members)
    {
        var recipientMember = members.FirstOrDefault(m => m.UserId == recipientUserId);
        var recipientPlatforms = recipientMember?.PlatformIds ?? Array.Empty<int>();
        var middlemanIds = FindMiddlemen(giverUserId, recipientUserId, members);
        var middlemanOptions = members
            .Where(m => middlemanIds.Contains(m.UserId))
            .Select(m =>
            {
                var sharedIds = giverPlatformIds.Intersect(m.PlatformIds).ToHashSet();
                return new MiddlemanOptionDto
                {
                    UserId = m.UserId,
                    Username = m.Username,
                    CommonPlatformIds = sharedIds.ToList(),
                    PlatformAccounts = m.PlatformAccounts
                        .Where(p => sharedIds.Contains(p.PlatformId))
                        .ToList()
                };
            })
            .ToList();

        var commonPlatformIds = giverPlatformIds.Intersect(recipientPlatforms).ToList();
        var hasDirectPlatform = commonPlatformIds.Count > 0;

        return new ReceptionOrderEntryDto
        {
            UserId = recipientUserId,
            Username = username,
            AmountNeeded = Math.Round(need, 2),
            EntryType = entryType,
            ThresholdId = thresholdId,
            CycleUserId = cycleUserId,
            SeasonCycleId = seasonCycleId,
            MiddlemanOptions = middlemanOptions,
            DefaultMiddlemanId = middlemanOptions.Count == 1 ? middlemanOptions[0].UserId : null,
            NoSuitableMiddleman = !hasDirectPlatform && middlemanOptions.Count == 0,
            GiverPlatformIds = giverPlatformIds.ToList(),
            RecipientPlatformIds = recipientPlatforms.ToList(),
            CommonPlatformIds = commonPlatformIds,
            RecipientPreferredPlatformName = recipientMember?.PreferredPlatformName,
            RecipientPreferredPlatformHandle = recipientMember?.PreferredPlatformHandle,
            RecipientPlatformAccounts = recipientMember?.PlatformAccounts
                .Where(p => commonPlatformIds.Contains(p.PlatformId))
                .ToList() ?? []
        };
    }

    private async Task<bool> TryStartSeasonIfReadyAsync(Crew crew, CancellationToken cancellationToken)
    {
        if (crew.SeasonStarted)
        {
            return false;
        }

        var readyMembers = await mutualAidRepository.GetSeasonReadyMembersAsync(crew.Id, cancellationToken);
        if (readyMembers.Count < 3)
        {
            return false;
        }

        await StartFirstSeasonAsync(crew, readyMembers, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        return true;
    }

    private async Task StartFirstSeasonAsync(
        Crew crew,
        IReadOnlyList<CrewMembership> readyMembers,
        CancellationToken cancellationToken)
    {
        if (readyMembers.Count < 3)
        {
            return;
        }

        crew.SeasonStarted = true;
        crew.CurrentSeasonStartDate = DateTime.UtcNow;

        foreach (var member in readyMembers)
        {
            member.IsInSeason = true;
        }

        await InitializeSeasonStateAsync(crew, readyMembers, cancellationToken);

        await AddCelebratoryGiftLogEntryAsync(
            crew,
            GiftType.SeasonStarted,
            actorUserId: crew.CreatedByUserId,
            cancellationToken);

        await notificationService.NotifyCrewAsync(
            crew.Id,
            NotificationKind.NewSeason,
            "New season",
            "A new mutual aid season has started.",
            "/app/crew/gift-log",
            cancellationToken: cancellationToken);
    }

    private async Task JoinActiveSeasonAsync(Crew crew, CrewMembership membership, CancellationToken cancellationToken)
    {
        membership.IsInSeason = true;
        membership.CurrentPriorityScore = await GetPriorityScoreForUserAsync(membership.UserId, crew.Id, cancellationToken);

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var isMember = await IsFinancialMemberAsync(membership.UserId, crew.Id, membership, cancellationToken);
        var cycleCap = GetEffectiveCycleCap(isMember, crew, capacityContext);

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate!.Value, cancellationToken);
        var insertPosition = GetInsertPositionForNewCycle(cycles, membership.CurrentPriorityScore);

        await mutualAidRepository.AddSeasonCycleAsync(new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = membership.UserId,
            SeasonStartDate = crew.CurrentSeasonStartDate!.Value,
            CycleCapAtStart = cycleCap,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = membership.CurrentPriorityScore,
            ReceptionOrderPosition = insertPosition,
            HasCycleStarted = false
        }, cancellationToken);
    }

    public async Task EnsureMemberInActiveSeasonAsync(
        int crewId,
        CrewMembership membership,
        CancellationToken cancellationToken = default)
    {
        if (membership.IsInSeason)
        {
            return;
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted)
        {
            return;
        }

        await JoinActiveSeasonAsync(crew, membership, cancellationToken);
    }

    private static int GetInsertPositionForNewCycle(
        IReadOnlyList<SeasonCycle> cycles,
        decimal priorityScore)
    {
        var (leader, runnerUp) = FindLockedLeaderAndRunnerUp(cycles);
        var minAllowed = GetMinInsertPosition(leader, runnerUp);

        var target = cycles
            .Where(c => IsPrimaryCycle(c) && !c.CycleCompleted && c.PriorityScoreAtSeasonStart > priorityScore)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault();

        if (target is null)
        {
            return Math.Max((cycles.Max(c => (int?)c.ReceptionOrderPosition) ?? -1) + 1, minAllowed);
        }

        return Math.Max(target.ReceptionOrderPosition, minAllowed);
    }

    private async Task InitializeSeasonStateAsync(
        Crew crew,
        IReadOnlyList<CrewMembership> participants,
        CancellationToken cancellationToken)
    {
        foreach (var member in participants)
        {
            if (member.User is not null)
            {
                member.User.PercentBonus = MutualAidCalculationService.GetSacrificePercentBonus(
                    member.EmergencySacrificesThisSeason);
            }

            member.EmergencySacrificesThisSeason = 0;
        }

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        crew.SeasonMemberCycleCap = capacityContext.MemberCycleCap;
        crew.SeasonNonMemberCycleCap = capacityContext.NonMemberCycleCap;

        var ordered = new List<(CrewMembership Member, decimal Score)>();
        foreach (var member in participants)
        {
            var score = await GetPriorityScoreForUserAsync(member.UserId, crew.Id, cancellationToken);
            member.CurrentPriorityScore = score;
            ordered.Add((member, score));
        }

        ordered = ordered.OrderByDescending(x => x.Score).ToList();
        var position = 0;
        foreach (var (member, score) in ordered)
        {
            var isMember = await IsFinancialMemberAsync(member.UserId, crew.Id, member, cancellationToken);
            var cycleCap = isMember ? capacityContext.MemberCycleCap : capacityContext.NonMemberCycleCap;

            await mutualAidRepository.AddSeasonCycleAsync(new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = member.UserId,
                SeasonStartDate = crew.CurrentSeasonStartDate!.Value,
                CycleCapAtStart = cycleCap,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = score,
                ReceptionOrderPosition = position++,
                HasCycleStarted = false
            }, cancellationToken);
        }

        await TryCreateFirstOfMonthThresholdsAsync(crew, cancellationToken);
    }

    private async Task TryCreateFirstOfMonthThresholdsAsync(Crew crew, CancellationToken cancellationToken)
    {
        if (!crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var now = DateTime.UtcNow;
        if (now.Day != 1)
        {
            return;
        }

        await CreateMonthlyThresholdsForMonthAsync(crew, now.Year, now.Month, cancellationToken);
    }

    private async Task CreateMonthlyThresholdsForMonthAsync(
        Crew crew,
        int year,
        int month,
        CancellationToken cancellationToken)
    {
        if (!AreSurvivalThresholdsEnabled(crew))
        {
            return;
        }

        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        if (capacityContext.SurvivalThresholdAmount <= 0)
        {
            return;
        }

        var survivalRecipients = participants
            .Where(m => m.User.NeedsSurvivalAid)
            .OrderByDescending(m => m.CurrentPriorityScore)
            .ToList();

        var created = false;
        var position = await mutualAidRepository.GetNextThresholdOrderPositionAsync(crew.Id, cancellationToken);
        foreach (var member in survivalRecipients)
        {
            if (await mutualAidRepository.HasThresholdForMonthAsync(crew.Id, member.UserId, year, month, cancellationToken))
            {
                continue;
            }

            await mutualAidRepository.AddThresholdAsync(new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = member.UserId,
                Year = year,
                Month = month,
                ThresholdAmount = capacityContext.SurvivalThresholdAmount,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = position++,
                Satisfied = false
            }, cancellationToken);
            created = true;
        }

        if (created)
        {
            await AddCelebratoryGiftLogEntryAsync(
                crew,
                GiftType.SurvivalThresholdsRefreshed,
                actorUserId: crew.CreatedByUserId,
                cancellationToken);

            await notificationService.NotifyCrewAsync(
                crew.Id,
                NotificationKind.SurvivalThresholdsRefreshed,
                "Survival thresholds refreshed",
                "Survival thresholds have been refreshed for the new month.",
                "/app/crew/gift-log",
                cancellationToken: cancellationToken);

            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private Task ApplyToThresholdAsync(Gift gift, CancellationToken cancellationToken) =>
        ApplyToThresholdForUserAsync(gift, gift.RecipientUserId, cancellationToken);

    private async Task ApplyToThresholdForUserAsync(Gift gift, int userId, CancellationToken cancellationToken)
    {
        var thresholds = await mutualAidRepository.GetUnsatisfiedThresholdsAsync(gift.CrewId, cancellationToken);
        var threshold = thresholds.FirstOrDefault(t => t.UserId == userId);
        if (threshold is null)
        {
            return;
        }

        threshold.ReceivedAmount += gift.Amount;
        if (threshold.ReceivedAmount >= threshold.ThresholdAmount)
        {
            threshold.Satisfied = true;
            threshold.ReceivedAmount = threshold.ThresholdAmount;
        }
    }

    private async Task ApplyCustomGiftWaterfallAsync(
        Gift gift,
        int recipientUserId,
        Crew crew,
        CancellationToken cancellationToken)
    {
        var remaining = gift.Amount;

        if (AreSurvivalThresholdsEnabled(crew))
        {
            var thresholds = (await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crew.Id, cancellationToken))
                .Where(t => t.UserId == recipientUserId)
                .OrderBy(t => t.Year)
                .ThenBy(t => t.Month)
                .ThenBy(t => t.ReceptionOrderPosition)
                .ToList();

            foreach (var threshold in thresholds)
            {
                if (remaining <= 0)
                {
                    break;
                }

                var need = threshold.ThresholdAmount - threshold.ReceivedAmount;
                if (need <= 0)
                {
                    continue;
                }

                var applied = Math.Min(remaining, need);
                threshold.ReceivedAmount += applied;
                remaining -= applied;
                if (threshold.ReceivedAmount >= threshold.ThresholdAmount)
                {
                    threshold.Satisfied = true;
                    threshold.ReceivedAmount = threshold.ThresholdAmount;
                }
            }
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
            crew.Id,
            crew.CurrentSeasonStartDate!.Value,
            cancellationToken);
        var activeCycle = cycles
            .Where(c => c.UserId == recipientUserId && c.HasCycleStarted && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault();

        var totalTarget = activeCycle
            ?? cycles
                .Where(c => c.UserId == recipientUserId && IsPrimaryCycle(c))
                .OrderBy(c => c.ReceptionOrderPosition)
                .FirstOrDefault()
            ?? cycles
                .Where(c => c.UserId == recipientUserId)
                .OrderBy(c => c.ReceptionOrderPosition)
                .FirstOrDefault();

        if (totalTarget is null)
        {
            return;
        }

        totalTarget.TotalReceptionAmount += gift.Amount;

        if (activeCycle is null || remaining <= 0)
        {
            return;
        }

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var membership = await membershipRepository.GetMembershipAsync(recipientUserId, crew.Id, cancellationToken);
        var isMember = membership is not null
            && await IsFinancialMemberAsync(recipientUserId, crew.Id, membership, cancellationToken);
        var effectiveCap = EmergencySplitService.ResolveSegmentCap(
            activeCycle,
            isMember,
            GetEffectiveCycleCap(true, crew, capacityContext),
            GetEffectiveCycleCap(false, crew, capacityContext));

        var room = Math.Max(0m, effectiveCap - activeCycle.CycleReceived);
        if (room > 0)
        {
            var applied = Math.Min(remaining, room);
            activeCycle.CycleReceived += applied;
            UpdateCycleCompletion(activeCycle, effectiveCap);
        }
    }

    private static void UpdateCycleCompletion(SeasonCycle cycle, decimal effectiveCap)
    {
        if (MutualAidCalculationService.IsCycleSatisfied(cycle, effectiveCap))
        {
            cycle.CycleCompleted = true;
            cycle.CycleCompletedAt ??= DateTime.UtcNow;
            cycle.CycleCapAtCompletion = effectiveCap;
            return;
        }

        // Keep completed cycles completed so catch-up stays virtual via GetCatchUpAmount.
        if (cycle.CycleCompleted)
        {
            return;
        }

        cycle.CycleCompleted = false;
        cycle.CycleCompletedAt = null;
    }

    private async Task TryEndSeasonAsync(Crew crew, CancellationToken cancellationToken)
    {
        if (!crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate.Value, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var effectiveMemberCap = GetEffectiveCycleCap(true, crew, capacityContext);
        var effectiveNonMemberCap = GetEffectiveCycleCap(false, crew, capacityContext);

        var allComplete = true;
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var memberStatus = new Dictionary<int, bool>();
        foreach (var participant in participants)
        {
            memberStatus[participant.UserId] = await IsFinancialMemberAsync(
                participant.UserId,
                crew.Id,
                participant,
                cancellationToken);
        }

        foreach (var cycle in cycles)
        {
            var isMember = memberStatus.GetValueOrDefault(cycle.UserId, false);
            var cap = EmergencySplitService.ResolveSegmentCap(
                cycle,
                isMember,
                effectiveMemberCap,
                effectiveNonMemberCap);
            if (!MutualAidCalculationService.IsCycleSatisfied(cycle, cap))
            {
                allComplete = false;
                break;
            }
        }

        if (!allComplete)
        {
            return;
        }

        crew.CurrentSeasonStartDate = DateTime.UtcNow;
        await InitializeSeasonStateAsync(crew, participants, cancellationToken);

        await AddCelebratoryGiftLogEntryAsync(
            crew,
            GiftType.SeasonStarted,
            actorUserId: crew.CreatedByUserId,
            cancellationToken);

        await notificationService.NotifyCrewAsync(
            crew.Id,
            NotificationKind.NewSeason,
            "New season",
            "A new mutual aid season has started.",
            "/app/crew/gift-log",
            cancellationToken: cancellationToken);
    }

    private async Task RepositionCrewmateInReceptionOrderAsync(
        Crew crew,
        int userId,
        decimal newPriorityScore,
        CancellationToken cancellationToken)
    {
        var cycles = (await mutualAidRepository.GetSeasonCyclesAsync(
            crew.Id,
            crew.CurrentSeasonStartDate!.Value,
            cancellationToken)).ToList();

        var affectedPrimary = cycles.FirstOrDefault(c =>
            c.UserId == userId && !c.CycleCompleted && IsPrimaryCycle(c));
        if (affectedPrimary is not null)
        {
            affectedPrimary.PriorityScoreAtSeasonStart = newPriorityScore;

            var (leader, runnerUp) = FindLockedLeaderAndRunnerUp(cycles);
            RepositionCycleUnit(cycles, affectedPrimary, newPriorityScore, leader, runnerUp);
        }

        await RepositionSurvivalThresholdsForUserAsync(crew.Id, userId, cancellationToken);
    }

    private static void RepositionCycleUnit(
        IReadOnlyList<SeasonCycle> allCycles,
        SeasonCycle affectedPrimary,
        decimal newPriorityScore,
        SeasonCycle? leader,
        SeasonCycle? runnerUp)
    {
        if (leader is not null && GetUnitPrimary(leader, allCycles)?.Id == affectedPrimary.Id)
        {
            return;
        }

        var incomplete = allCycles
            .Where(c => !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToList();
        var units = BuildIncompleteUnits(incomplete);
        if (units.Count <= 1)
        {
            return;
        }

        var affectedIndex = units.FindIndex(u => u.Any(c => c.Id == affectedPrimary.Id));
        if (affectedIndex < 0)
        {
            return;
        }

        var affectedUnit = units[affectedIndex];
        var withoutAffected = units.Where((_, i) => i != affectedIndex).ToList();

        var insertIndex = withoutAffected.Count;
        for (var i = 0; i < withoutAffected.Count; i++)
        {
            var otherPrimary = withoutAffected[i].LastOrDefault(IsPrimaryCycle);
            var otherScore = otherPrimary?.PriorityScoreAtSeasonStart
                ?? withoutAffected[i][0].PriorityScoreAtSeasonStart;
            if (otherScore < newPriorityScore)
            {
                insertIndex = i;
                break;
            }
        }

        var lockedUnitCount = 0;
        if (leader is not null)
        {
            lockedUnitCount = 1;
            if (runnerUp is not null
                && GetUnitPrimary(runnerUp, allCycles)?.Id != affectedPrimary.Id)
            {
                lockedUnitCount = 2;
            }
        }

        if (insertIndex < lockedUnitCount)
        {
            insertIndex = lockedUnitCount;
        }

        withoutAffected.Insert(insertIndex, affectedUnit);

        var position = incomplete.Min(c => c.ReceptionOrderPosition);
        foreach (var unit in withoutAffected)
        {
            foreach (var cycle in unit.OrderBy(c => c.ReceptionOrderPosition))
            {
                cycle.ReceptionOrderPosition = position++;
            }
        }
    }

    private async Task RepositionSurvivalThresholdsForUserAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken)
    {
        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null || !AreSurvivalThresholdsEnabled(crew))
        {
            return;
        }

        var thresholds = (await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crewId, cancellationToken)).ToList();
        var userThresholds = thresholds.Where(t => t.UserId == userId).ToList();
        if (userThresholds.Count == 0)
        {
            return;
        }

        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crewId, cancellationToken);
        decimal GetScore(int uid) =>
            participants.FirstOrDefault(p => p.UserId == uid)?.CurrentPriorityScore ?? 0m;

        foreach (var userThreshold in userThresholds)
        {
            var sameMonth = thresholds
                .Where(t => t.Year == userThreshold.Year && t.Month == userThreshold.Month)
                .OrderBy(t => t.ReceptionOrderPosition)
                .ToList();

            if (sameMonth.Count <= 1)
            {
                continue;
            }

            var newScore = GetScore(userId);
            var oldPosition = userThreshold.ReceptionOrderPosition;
            var withoutAffected = sameMonth.Where(t => t.UserId != userId).ToList();

            var insertIndex = withoutAffected.Count;
            for (var i = 0; i < withoutAffected.Count; i++)
            {
                if (GetScore(withoutAffected[i].UserId) < newScore)
                {
                    insertIndex = i;
                    break;
                }
            }

            var newPosition = insertIndex >= withoutAffected.Count
                ? withoutAffected.Max(t => t.ReceptionOrderPosition)
                : withoutAffected[insertIndex].ReceptionOrderPosition;

            if (newPosition == oldPosition)
            {
                continue;
            }

            if (oldPosition < newPosition)
            {
                foreach (var threshold in sameMonth.Where(t =>
                    t.ReceptionOrderPosition > oldPosition && t.ReceptionOrderPosition <= newPosition))
                {
                    threshold.ReceptionOrderPosition--;
                }
            }
            else
            {
                foreach (var threshold in sameMonth.Where(t =>
                    t.ReceptionOrderPosition >= newPosition && t.ReceptionOrderPosition < oldPosition))
                {
                    threshold.ReceptionOrderPosition++;
                }
            }

            userThreshold.ReceptionOrderPosition = newPosition;
        }
    }

    public async Task<decimal> GetCrewMonthlyGivingCapacityAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crewId, cancellationToken);
        if (participants.Count == 0)
        {
            participants = await mutualAidRepository.GetSeasonReadyMembersAsync(crewId, cancellationToken);
        }

        return MutualAidCalculationService.GetTotalMonthlyContributions(
            participants.Select(m => m.EstimatedMonthlyContribution ?? 0m));
    }

    private async Task<CapacityContext> BuildCapacityContextAsync(Crew crew, CancellationToken cancellationToken)
    {
        var totalContributions = await GetCrewMonthlyGivingCapacityAsync(crew.Id, cancellationToken);

        var thresholdRecipients = AreSurvivalThresholdsEnabled(crew)
            ? (await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken)).Count(m => m.User.NeedsSurvivalAid)
            : 0;
        var survivalThreshold = MutualAidCalculationService.GetSurvivalThresholdAmount(totalContributions, thresholdRecipients);
        var memberCap = MutualAidCalculationService.GetMemberCycleCap(crew, totalContributions);
        var nonMemberCap = MutualAidCalculationService.GetNonMemberCycleCap(crew, totalContributions);

        return new CapacityContext
        {
            TotalMonthlyGivingCapacity = totalContributions,
            SurvivalThresholdAmount = survivalThreshold,
            MemberCycleCap = memberCap,
            NonMemberCycleCap = nonMemberCap
        };
    }

    private static decimal GetEffectiveCycleCap(bool isMember, Crew crew, CapacityContext capacityContext)
    {
        var currentCap = isMember ? capacityContext.MemberCycleCap : capacityContext.NonMemberCycleCap;
        var seasonStartCap = isMember ? crew.SeasonMemberCycleCap : crew.SeasonNonMemberCycleCap;

        if (seasonStartCap <= 0m)
        {
            return currentCap;
        }

        return isMember
            ? MutualAidCalculationService.GetEffectiveMemberCycleCap(seasonStartCap, currentCap)
            : MutualAidCalculationService.GetEffectiveNonMemberCycleCap(seasonStartCap, currentCap);
    }

    private async Task<decimal> GetEffectiveCycleCapForUserAsync(
        int userId,
        Crew crew,
        CapacityContext capacityContext,
        CancellationToken cancellationToken)
    {
        var members = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var membership = members.FirstOrDefault(m => m.UserId == userId);
        if (membership is null)
        {
            return 0m;
        }

        var isMember = await IsFinancialMemberAsync(userId, crew.Id, membership, cancellationToken);
        return GetEffectiveCycleCap(isMember, crew, capacityContext);
    }

    private async Task RefreshHasCycleStartedForCrewAsync(Crew crew, CancellationToken cancellationToken)
    {
        if (!crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
            crew.Id,
            crew.CurrentSeasonStartDate.Value,
            cancellationToken);

        var incomplete = cycles
            .Where(c => !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToList();

        await RefreshHasCycleStartedAsync(crew, incomplete, cancellationToken);
    }

    private async Task RefreshHasCycleStartedAsync(
        Crew crew,
        IReadOnlyList<SeasonCycle> incompleteOrdered,
        CancellationToken cancellationToken)
    {
        // Frontmost among recipients who still need aid; non-needers never count as started.
        var frontmost = incompleteOrdered.FirstOrDefault(c => c.User?.InNeedOfAid != false);
        var changed = false;

        foreach (var cycle in incompleteOrdered)
        {
            var shouldBeStarted = frontmost is not null && cycle.Id == frontmost.Id;
            if (cycle.HasCycleStarted == shouldBeStarted)
            {
                continue;
            }

            var newlyStarted = shouldBeStarted && !cycle.HasCycleStarted;
            cycle.HasCycleStarted = shouldBeStarted;
            changed = true;

            if (newlyStarted)
            {
                await AddCelebratoryGiftLogEntryAsync(
                    crew,
                    GiftType.CycleStarted,
                    actorUserId: cycle.UserId,
                    cancellationToken);

                await notificationService.NotifyCrewAsync(
                    crew.Id,
                    NotificationKind.NewCycle,
                    "New cycle",
                    "A crewmate's reception cycle has started.",
                    "/app/crew/gift-log",
                    relatedEntityId: cycle.UserId,
                    cancellationToken: cancellationToken);
            }
        }

        if (changed)
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
    }

    private async Task<SeasonCycle?> FindPrimaryCycleForUserAsync(
        int crewId,
        int userId,
        DateTime seasonStartDate,
        CancellationToken cancellationToken)
    {
        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crewId, seasonStartDate, cancellationToken);
        return cycles
            .Where(c => c.UserId == userId && IsPrimaryCycle(c))
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault();
    }

    private static bool IsPrimaryCycle(SeasonCycle cycle) =>
        !cycle.EmergencyRequestId.HasValue && !cycle.EmergencySplitOfferId.HasValue;

    private static bool IsBoundSegment(SeasonCycle cycle) =>
        cycle.EmergencyRequestId.HasValue || cycle.EmergencySplitOfferId.HasValue;

    private static List<List<SeasonCycle>> BuildIncompleteUnits(IReadOnlyList<SeasonCycle> incompleteOrdered)
    {
        var units = new List<List<SeasonCycle>>();
        var i = 0;
        while (i < incompleteOrdered.Count)
        {
            var unit = new List<SeasonCycle>();
            while (i < incompleteOrdered.Count && IsBoundSegment(incompleteOrdered[i]))
            {
                unit.Add(incompleteOrdered[i]);
                i++;
            }

            if (i < incompleteOrdered.Count && IsPrimaryCycle(incompleteOrdered[i]))
            {
                unit.Add(incompleteOrdered[i]);
                i++;
            }

            if (unit.Count > 0)
            {
                units.Add(unit);
            }
        }

        return units;
    }

    private static SeasonCycle? GetUnitPrimary(SeasonCycle cycleInUnit, IReadOnlyList<SeasonCycle> allCycles)
    {
        if (IsPrimaryCycle(cycleInUnit))
        {
            return cycleInUnit;
        }

        var ordered = allCycles
            .Where(c => !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToList();
        var index = ordered.FindIndex(c => c.Id == cycleInUnit.Id);
        if (index < 0)
        {
            return null;
        }

        for (var i = index + 1; i < ordered.Count; i++)
        {
            if (IsPrimaryCycle(ordered[i]))
            {
                return ordered[i];
            }

            if (!IsBoundSegment(ordered[i]))
            {
                break;
            }
        }

        return null;
    }

    private static (SeasonCycle? Leader, SeasonCycle? RunnerUp) FindLockedLeaderAndRunnerUp(
        IReadOnlyList<SeasonCycle> cycles)
    {
        var incomplete = cycles
            .Where(c => !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToList();
        if (incomplete.Count == 0)
        {
            return (null, null);
        }

        var units = BuildIncompleteUnits(incomplete);
        if (units.Count == 0)
        {
            return (null, null);
        }

        var startedUnit = units.FirstOrDefault(u => u.Any(c => c.HasCycleStarted));
        var leaderUnit = startedUnit ?? units[0];
        var leaderPrimary = leaderUnit.LastOrDefault(IsPrimaryCycle) ?? leaderUnit[^1];

        SeasonCycle? runnerUp = null;
        var leaderIndex = units.FindIndex(u => u.Any(c => c.Id == leaderPrimary.Id));
        if (leaderIndex >= 0 && leaderIndex + 1 < units.Count)
        {
            var runnerUpUnit = units[leaderIndex + 1];
            runnerUp = runnerUpUnit.LastOrDefault(IsPrimaryCycle) ?? runnerUpUnit[^1];
        }

        return (leaderPrimary, runnerUp);
    }

    private static int GetMinInsertPosition(SeasonCycle? leader, SeasonCycle? runnerUp)
    {
        if (runnerUp is not null)
        {
            return runnerUp.ReceptionOrderPosition + 1;
        }

        if (leader is not null)
        {
            return leader.ReceptionOrderPosition + 1;
        }

        return 0;
    }

    private static bool AreSurvivalThresholdsEnabled(Crew crew) => crew.AllowSurvivalThresholds;

    private async Task AddCelebratoryGiftLogEntryAsync(
        Crew crew,
        GiftType type,
        int actorUserId,
        CancellationToken cancellationToken)
    {
        await giftRepository.AddAsync(new Gift
        {
            CrewId = crew.Id,
            GiverUserId = actorUserId,
            RecipientUserId = actorUserId,
            Type = type,
            Amount = 0m,
            CrewPaymentPlatformId = null,
            CreatedAt = DateTime.UtcNow,
            IsCustomGift = true,
            CountsTowardReception = false,
            CountsTowardContribution = false,
            ReceptionApplied = true,
            VerificationStatus = GiftVerificationStatus.Verified
        }, cancellationToken);
    }

    private sealed class CapacityContext
    {
        public decimal TotalMonthlyGivingCapacity { get; init; }
        public decimal SurvivalThresholdAmount { get; init; }
        public decimal MemberCycleCap { get; init; }
        public decimal NonMemberCycleCap { get; init; }
    }
}
