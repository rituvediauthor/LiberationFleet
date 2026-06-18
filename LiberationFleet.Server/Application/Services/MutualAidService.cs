using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Services;

public class MutualAidService(
    IMutualAidRepository mutualAidRepository,
    ICrewMembershipRepository membershipRepository,
    IUnitOfWork unitOfWork) : IMutualAidService
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
        CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return Array.Empty<ReceptionOrderEntryDto>();
        }

        await EnsureMonthlyThresholdsAsync(crew, cancellationToken);

        var allMembers = await mutualAidRepository.GetActiveMembersWithUsersAsync(crew.Id, cancellationToken);
        var memberPlatforms = allMembers.Select(m => new CrewMemberPlatforms
        {
            UserId = m.UserId,
            Username = m.User.Username,
            PlatformIds = m.User.PaymentPlatforms.Select(p => p.PaymentPlatformId).ToList()
        }).ToList();

        var giverPlatforms = memberPlatforms.FirstOrDefault(m => m.UserId == userId)?.PlatformIds ?? Array.Empty<int>();
        var entries = new List<ReceptionOrderEntryDto>();

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
                userId,
                giverPlatforms,
                memberPlatforms));
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate.Value, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var seasonParticipants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var memberStatus = new Dictionary<int, bool>();
        foreach (var participant in seasonParticipants)
        {
            memberStatus[participant.UserId] = await IsFinancialMemberAsync(participant.UserId, crew.Id, participant, cancellationToken);
        }

        foreach (var cycle in cycles.OrderBy(c => c.ReceptionOrderPosition))
        {
            var isMember = memberStatus.GetValueOrDefault(cycle.UserId, false);
            var effectiveCap = GetEffectiveCycleCap(isMember, crew, capacityContext);
            if (MutualAidCalculationService.IsCycleSatisfied(cycle, effectiveCap))
            {
                continue;
            }

            var need = effectiveCap - cycle.CycleReceived;
            if (need <= 0)
            {
                continue;
            }

            var username = cycle.User?.Username
                ?? allMembers.FirstOrDefault(m => m.UserId == cycle.UserId)?.User.Username
                ?? string.Empty;

            entries.Add(BuildEntry(
                cycle.UserId,
                username,
                need,
                "cycle",
                null,
                cycle.UserId,
                userId,
                giverPlatforms,
                memberPlatforms));
        }

        return entries.Take(limit).ToList();
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

        var seasonStarted = false;
        if (!crew.SeasonStarted)
        {
            var readyCount = members.Count(m => m.IsSeasonReady || m.UserId == userId);
            if (readyCount >= 3)
            {
                await StartFirstSeasonAsync(crew, cancellationToken);
                seasonStarted = true;
            }
        }
        else if (!membership.IsInSeason)
        {
            await JoinActiveSeasonAsync(crew, membership, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
        if (!gift.CountsTowardReception || gift.IsCustomGift)
        {
            return;
        }

        var crew = await mutualAidRepository.GetCrewAsync(gift.CrewId, cancellationToken);
        if (crew is null || !crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var cycle = await mutualAidRepository.GetSeasonCycleAsync(
            gift.CrewId,
            gift.RecipientUserId,
            crew.CurrentSeasonStartDate.Value,
            cancellationToken);

        if (cycle is null)
        {
            return;
        }

        cycle.TotalReceptionAmount += gift.Amount;

        if (gift.IsSurvivalThreshold)
        {
            cycle.SurvivalThresholdReceived += gift.Amount;
            await ApplyToThresholdAsync(gift, cancellationToken);
        }
        else
        {
            var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
            var effectiveCap = await GetEffectiveCycleCapForUserAsync(gift.RecipientUserId, crew, capacityContext, cancellationToken);

            if (cycle.TotalReceptionAmount - gift.Amount < effectiveCap)
            {
                var cycleRoom = effectiveCap - cycle.CycleReceived;
                if (cycleRoom > 0)
                {
                    var applied = Math.Min(gift.Amount, cycleRoom);
                    cycle.CycleReceived += applied;
                    if (applied > 0)
                    {
                        cycle.HasCycleStarted = true;
                    }
                }
            }
        }

        var capacity = await BuildCapacityContextAsync(crew, cancellationToken);
        var cap = await GetEffectiveCycleCapForUserAsync(gift.RecipientUserId, crew, capacity, cancellationToken);
        UpdateCycleCompletion(cycle, cap);

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

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate.Value, cancellationToken);

        foreach (var cycle in cycles)
        {
            var cap = await GetEffectiveCycleCapForUserAsync(cycle.UserId, crew, capacityContext, cancellationToken);
            UpdateCycleCompletion(cycle, cap);
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
        if (membership.IsHonoraryMember)
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
            .Where(m => m.UserId != giverUserId
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
        int giverUserId,
        IReadOnlyList<int> giverPlatformIds,
        IReadOnlyList<CrewMemberPlatforms> members)
    {
        var recipientPlatforms = members.FirstOrDefault(m => m.UserId == recipientUserId)?.PlatformIds ?? Array.Empty<int>();
        var middlemanIds = FindMiddlemen(giverUserId, recipientUserId, members);
        var middlemanOptions = members
            .Where(m => middlemanIds.Contains(m.UserId))
            .Select(m => new MiddlemanOptionDto { UserId = m.UserId, Username = m.Username })
            .ToList();

        var hasDirectPlatform = giverPlatformIds.Intersect(recipientPlatforms).Any();

        return new ReceptionOrderEntryDto
        {
            UserId = recipientUserId,
            Username = username,
            AmountNeeded = Math.Round(need, 2),
            EntryType = entryType,
            ThresholdId = thresholdId,
            CycleUserId = cycleUserId,
            MiddlemanOptions = middlemanOptions,
            DefaultMiddlemanId = middlemanOptions.Count == 1 ? middlemanOptions[0].UserId : null,
            NoSuitableMiddleman = !hasDirectPlatform && middlemanOptions.Count == 0,
            GiverPlatformIds = giverPlatformIds.ToList(),
            RecipientPlatformIds = recipientPlatforms.ToList()
        };
    }

    private async Task StartFirstSeasonAsync(Crew crew, CancellationToken cancellationToken)
    {
        var readyMembers = await mutualAidRepository.GetSeasonReadyMembersAsync(crew.Id, cancellationToken);
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
    }

    private async Task JoinActiveSeasonAsync(Crew crew, CrewMembership membership, CancellationToken cancellationToken)
    {
        membership.IsInSeason = true;
        membership.CurrentPriorityScore = await GetPriorityScoreForUserAsync(membership.UserId, crew.Id, cancellationToken);

        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);
        var isMember = await IsFinancialMemberAsync(membership.UserId, crew.Id, membership, cancellationToken);
        var cycleCap = isMember ? capacityContext.MemberCycleCap : capacityContext.NonMemberCycleCap;

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

    private static int GetInsertPositionForNewCycle(
        IReadOnlyList<SeasonCycle> cycles,
        decimal priorityScore)
    {
        var leader = cycles
            .Where(c => c.HasCycleStarted && !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault();

        var minAllowed = leader?.ReceptionOrderPosition ?? 0;

        var target = cycles
            .Where(c => !c.CycleCompleted && c.PriorityScoreAtSeasonStart > priorityScore)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault();

        if (target is null)
        {
            return (cycles.Max(c => (int?)c.ReceptionOrderPosition) ?? -1) + 1;
        }

        return Math.Max(target.ReceptionOrderPosition, minAllowed + 1);
    }

    private async Task InitializeSeasonStateAsync(
        Crew crew,
        IReadOnlyList<CrewMembership> participants,
        CancellationToken cancellationToken)
    {
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

        await CreateMonthlyThresholdsForParticipantsAsync(crew, participants, capacityContext, cancellationToken);
    }

    private async Task EnsureMonthlyThresholdsAsync(Crew crew, CancellationToken cancellationToken)
    {
        if (!crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);

        foreach (var member in participants.Where(m => m.User.NeedsSurvivalAid))
        {
            if (await mutualAidRepository.HasThresholdForMonthAsync(crew.Id, member.UserId, now.Year, now.Month, cancellationToken))
            {
                continue;
            }

            var nextPosition = await mutualAidRepository.GetNextThresholdOrderPositionAsync(crew.Id, cancellationToken);
            await mutualAidRepository.AddThresholdAsync(new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = member.UserId,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = capacityContext.SurvivalThresholdAmount,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = nextPosition,
                Satisfied = false
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
    }

    private async Task CreateMonthlyThresholdsForParticipantsAsync(
        Crew crew,
        IReadOnlyList<CrewMembership> participants,
        CapacityContext capacityContext,
        CancellationToken cancellationToken)
    {
        if (capacityContext.SurvivalThresholdAmount <= 0)
        {
            return;
        }

        var now = DateTime.UtcNow;
        var survivalRecipients = participants
            .Where(m => m.User.NeedsSurvivalAid)
            .OrderByDescending(m => m.CurrentPriorityScore)
            .ToList();

        var position = 0;
        foreach (var member in survivalRecipients)
        {
            await mutualAidRepository.AddThresholdAsync(new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = member.UserId,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = capacityContext.SurvivalThresholdAmount,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = position++,
                Satisfied = false
            }, cancellationToken);
        }
    }

    private async Task ApplyToThresholdAsync(Gift gift, CancellationToken cancellationToken)
    {
        var thresholds = await mutualAidRepository.GetUnsatisfiedThresholdsAsync(gift.CrewId, cancellationToken);
        var threshold = thresholds.FirstOrDefault(t => t.UserId == gift.RecipientUserId);
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

    private static void UpdateCycleCompletion(SeasonCycle cycle, decimal effectiveCap)
    {
        if (MutualAidCalculationService.IsCycleSatisfied(cycle, effectiveCap))
        {
            cycle.CycleCompleted = true;
            cycle.CycleCompletedAt ??= DateTime.UtcNow;
        }
        else
        {
            cycle.CycleCompleted = false;
            cycle.CycleCompletedAt = null;
        }
    }

    private async Task TryEndSeasonAsync(Crew crew, CancellationToken cancellationToken)
    {
        if (!crew.CurrentSeasonStartDate.HasValue)
        {
            return;
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate.Value, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);

        var allComplete = true;
        foreach (var cycle in cycles)
        {
            var cap = await GetEffectiveCycleCapForUserAsync(cycle.UserId, crew, capacityContext, cancellationToken);
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
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        await InitializeSeasonStateAsync(crew, participants, cancellationToken);
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

        var affectedCycle = cycles.FirstOrDefault(c => c.UserId == userId && !c.CycleCompleted);
        if (affectedCycle is not null)
        {
            affectedCycle.PriorityScoreAtSeasonStart = newPriorityScore;

            var leader = cycles
                .Where(c => c.HasCycleStarted && !c.CycleCompleted)
                .OrderBy(c => c.ReceptionOrderPosition)
                .FirstOrDefault();

            RepositionSingleCycle(cycles, affectedCycle, newPriorityScore, leader);
        }

        await RepositionSurvivalThresholdsForUserAsync(crew.Id, userId, cancellationToken);
    }

    private static void RepositionSingleCycle(
        IReadOnlyList<SeasonCycle> allCycles,
        SeasonCycle affected,
        decimal newPriorityScore,
        SeasonCycle? leader)
    {
        if (leader is not null && leader.UserId == affected.UserId)
        {
            return;
        }

        var incomplete = allCycles
            .Where(c => !c.CycleCompleted)
            .OrderBy(c => c.ReceptionOrderPosition)
            .ToList();

        if (incomplete.Count <= 1)
        {
            return;
        }

        var oldPosition = affected.ReceptionOrderPosition;
        var withoutAffected = incomplete.Where(c => c.UserId != affected.UserId).ToList();

        var insertIndex = withoutAffected.Count;
        for (var i = 0; i < withoutAffected.Count; i++)
        {
            if (withoutAffected[i].PriorityScoreAtSeasonStart < newPriorityScore)
            {
                insertIndex = i;
                break;
            }
        }

        var newPosition = insertIndex >= withoutAffected.Count
            ? withoutAffected.Max(c => c.ReceptionOrderPosition)
            : withoutAffected[insertIndex].ReceptionOrderPosition;

        if (leader is not null)
        {
            var minAllowed = leader.ReceptionOrderPosition + 1;
            if (newPosition < minAllowed)
            {
                newPosition = minAllowed;
            }
        }

        if (newPosition == oldPosition)
        {
            return;
        }

        if (oldPosition < newPosition)
        {
            foreach (var cycle in incomplete.Where(c =>
                c.ReceptionOrderPosition > oldPosition && c.ReceptionOrderPosition <= newPosition))
            {
                cycle.ReceptionOrderPosition--;
            }
        }
        else
        {
            foreach (var cycle in incomplete.Where(c =>
                c.ReceptionOrderPosition >= newPosition && c.ReceptionOrderPosition < oldPosition))
            {
                cycle.ReceptionOrderPosition++;
            }
        }

        affected.ReceptionOrderPosition = newPosition;
    }

    private async Task RepositionSurvivalThresholdsForUserAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken)
    {
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

    private async Task<CapacityContext> BuildCapacityContextAsync(Crew crew, CancellationToken cancellationToken)
    {
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        if (participants.Count == 0)
        {
            participants = await mutualAidRepository.GetSeasonReadyMembersAsync(crew.Id, cancellationToken);
        }

        decimal totalCapacity = 0m;
        foreach (var member in participants)
        {
            var contributions = await mutualAidRepository.GetContributionsLast3MonthsAsync(member.UserId, crew.Id, cancellationToken);
            totalCapacity += MutualAidCalculationService.CalculateMonthlyGivingCapacity(
                contributions,
                member.EstimatedMonthlyContribution);
        }

        var thresholdRecipients = participants.Count(m => m.User.NeedsSurvivalAid);
        var survivalThreshold = MutualAidCalculationService.GetSurvivalThresholdAmount(totalCapacity, thresholdRecipients);
        var memberCap = MutualAidCalculationService.GetMemberCycleCap(totalCapacity);
        var nonMemberCap = MutualAidCalculationService.GetNonMemberCycleCap(totalCapacity);

        return new CapacityContext
        {
            TotalMonthlyGivingCapacity = totalCapacity,
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

    private sealed class CapacityContext
    {
        public decimal TotalMonthlyGivingCapacity { get; init; }
        public decimal SurvivalThresholdAmount { get; init; }
        public decimal MemberCycleCap { get; init; }
        public decimal NonMemberCycleCap { get; init; }
    }
}
