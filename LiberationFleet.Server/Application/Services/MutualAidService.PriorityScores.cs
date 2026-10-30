using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public partial class MutualAidService
{
    public async Task<(PriorityScoreBreakdown GivingSeason, PriorityScoreBreakdown Library)> GetProfilePriorityBreakdownsAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var inputs = await LoadPriorityInputsAsync(
            userId,
            crewId,
            excludeActiveSeasonContributions: false,
            seasonStartAnchor: null,
            cancellationToken);
        if (inputs is null)
        {
            var empty = EmptyPriorityBreakdown();
            return (empty, empty);
        }

        var givingByRole = CrewRoleMapper.HasAnyRole(inputs.Membership);
        var libraryByRole = CrewRoleMapper.HasAnyRoleExceptOrganizer(inputs.Membership);
        bool givingFinancial;
        bool libraryFinancial;
        if (libraryByRole)
        {
            givingFinancial = true;
            libraryFinancial = true;
        }
        else if (givingByRole)
        {
            givingFinancial = true;
            libraryFinancial = await IsFinancialMemberAsync(
                userId,
                crewId,
                inputs.Membership,
                cancellationToken,
                ignoreOrganizerRole: true);
        }
        else
        {
            givingFinancial = await IsFinancialMemberAsync(
                userId,
                crewId,
                inputs.Membership,
                cancellationToken);
            libraryFinancial = givingFinancial;
        }

        return (
            MutualAidCalculationService.CalculatePriorityScoreBreakdown(
                inputs.Membership.User,
                inputs.Membership,
                givingFinancial,
                inputs.CrewLifetime,
                inputs.UserLifetime,
                inputs.SurvivalThresholdAmount,
                demoteOrganizerToLastPlace: true),
            MutualAidCalculationService.CalculatePriorityScoreBreakdown(
                inputs.Membership.User,
                inputs.Membership,
                libraryFinancial,
                inputs.CrewLifetime,
                inputs.UserLifetime,
                inputs.SurvivalThresholdAmount,
                demoteOrganizerToLastPlace: false));
    }

    public Task<IReadOnlyDictionary<int, decimal>> GetLibraryPriorityScoresForMembersAsync(
        IReadOnlyList<CrewMembership> members,
        CancellationToken cancellationToken = default) =>
        ScoreMembersForPriorityAsync(
            members,
            assumeInNeedNonOrganizerForLot: true,
            seasonStartAnchor: null,
            cancellationToken);

    public Task<IReadOnlyDictionary<int, decimal>> GetGivingPriorityScoresForMembersAsync(
        IReadOnlyList<CrewMembership> members,
        DateTime? seasonStartAnchor = null,
        CancellationToken cancellationToken = default) =>
        ScoreMembersForPriorityAsync(
            members,
            assumeInNeedNonOrganizerForLot: false,
            seasonStartAnchor,
            cancellationToken);

    private async Task<IReadOnlyDictionary<int, decimal>> ScoreMembersForPriorityAsync(
        IReadOnlyList<CrewMembership> members,
        bool assumeInNeedNonOrganizerForLot,
        DateTime? seasonStartAnchor,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<int, decimal>();
        if (members.Count == 0)
        {
            return scores;
        }

        foreach (var crewGroup in members.GroupBy(m => m.CrewId))
        {
            var crewScores = await ScoreCrewMembersForPriorityAsync(
                crewGroup.Key,
                crewGroup.ToList(),
                assumeInNeedNonOrganizerForLot,
                seasonStartAnchor,
                cancellationToken);
            foreach (var (userId, score) in crewScores)
            {
                scores.TryAdd(userId, score);
            }
        }

        return scores;
    }

    private async Task<PriorityInputs?> LoadPriorityInputsAsync(
        int userId,
        int crewId,
        bool excludeActiveSeasonContributions,
        DateTime? seasonStartAnchor,
        CancellationToken cancellationToken)
    {
        var membership = await mutualAidRepository.GetMembershipWithUserAsync(userId, crewId, cancellationToken);
        if (membership?.User is null)
        {
            return null;
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

        var anchor = seasonStartAnchor
            ?? crew?.CurrentSeasonStartDate
            ?? DateTime.UtcNow;
        if (await IsCrewFirstGivingSeasonAsync(crewId, anchor, cancellationToken))
        {
            (crewLifetime, userLifetime) = await ApplyFirstSeasonEstimateSeedsAsync(
                crewId,
                userId,
                userLifetime,
                membership.EstimatedMonthlyContribution,
                contributionCutoff,
                cancellationToken);
        }

        if (membership.IsHonoraryMember)
        {
            var peerAverage = await GetHonoraryPeerAverageContributionsAsync(
                crewId,
                userId,
                contributionCutoff,
                cancellationToken);
            if (peerAverage > userLifetime)
            {
                userLifetime = peerAverage;
            }
        }

        var capacityContext = crew is not null
            ? await BuildCapacityContextAsync(crew, cancellationToken)
            : new CapacityContext();

        return new PriorityInputs(membership, crewLifetime, userLifetime, capacityContext.SurvivalThresholdAmount);
    }

    /// <summary>
    /// Same Library of Things score as <see cref="GetPriorityScoreForUserAsync"/> with
    /// <c>assumeInNeedNonOrganizerForLot: true</c>, but crew-wide reads happen once.
    /// </summary>
    private async Task<Dictionary<int, decimal>> ScoreCrewMembersForPriorityAsync(
        int crewId,
        IReadOnlyList<CrewMembership> members,
        bool assumeInNeedNonOrganizerForLot,
        DateTime? seasonStartAnchor,
        CancellationToken cancellationToken)
    {
        var scores = new Dictionary<int, decimal>();
        var pending = new List<CrewMembership>();
        foreach (var member in members)
        {
            if (scores.ContainsKey(member.UserId) || pending.Any(m => m.UserId == member.UserId))
            {
                continue;
            }

            if (member.User is null)
            {
                scores[member.UserId] = await GetPriorityScoreForUserAsync(
                    member.UserId,
                    crewId,
                    cancellationToken,
                    assumeInNeedNonOrganizerForLot: assumeInNeedNonOrganizerForLot,
                    seasonStartAnchor: seasonStartAnchor);
                continue;
            }

            pending.Add(member);
        }

        if (pending.Count == 0)
        {
            return scores;
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        var rawLifetimes = await mutualAidRepository.GetLifetimeContributionsForUsersAsync(
            crewId,
            pending.Select(m => m.UserId).ToList(),
            before: null,
            cancellationToken);
        var crewLifetime = await mutualAidRepository.GetCrewLifetimeContributionsAsync(
            crewId,
            before: null,
            cancellationToken);

        var anchor = seasonStartAnchor
            ?? crew?.CurrentSeasonStartDate
            ?? DateTime.UtcNow;
        var firstSeason = await IsCrewFirstGivingSeasonAsync(crewId, anchor, cancellationToken);
        IReadOnlyList<CrewMembership> seasonMembers = [];
        IReadOnlyDictionary<int, decimal> seasonLifetimes = new Dictionary<int, decimal>();
        decimal seededCrewLifetime = 0m;
        if (firstSeason)
        {
            seasonMembers = await mutualAidRepository.GetSeasonContributionMembersAsync(crewId, cancellationToken);
            if (seasonMembers.Count > 0)
            {
                seasonLifetimes = await mutualAidRepository.GetLifetimeContributionsForUsersAsync(
                    crewId,
                    seasonMembers.Select(m => m.UserId).ToList(),
                    before: null,
                    cancellationToken);
                foreach (var seasonMember in seasonMembers)
                {
                    var lifetime = seasonLifetimes.GetValueOrDefault(seasonMember.UserId);
                    var estimate = seasonMember.EstimatedMonthlyContribution ?? 0m;
                    seededCrewLifetime += Math.Max(lifetime, estimate);
                }
            }
        }

        Dictionary<int, decimal>? peerLifetimes = null;
        if (pending.Any(m => m.IsHonoraryMember))
        {
            var peers = await mutualAidRepository.GetActiveMembersWithUsersAsync(crewId, cancellationToken);
            var peerIds = peers.Where(p => !p.IsBanned).Select(p => p.UserId).Distinct().ToList();
            var loaded = await mutualAidRepository.GetLifetimeContributionsForUsersAsync(
                crewId,
                peerIds,
                before: null,
                cancellationToken);
            peerLifetimes = peerIds.ToDictionary(id => id, id => loaded.GetValueOrDefault(id));
        }

        var capacityContext = crew is not null
            ? await BuildCapacityContextAsync(crew, cancellationToken)
            : new CapacityContext();

        IReadOnlyDictionary<int, IReadOnlyDictionary<(int Year, int Month), decimal>>? contributionsByUser = null;
        IReadOnlyList<(int Year, int Month)>? months = null;
        if (pending.Any(m => !HasPriorityFinancialRole(m, assumeInNeedNonOrganizerForLot)))
        {
            var utcNow = DateTime.UtcNow;
            months = MutualAidCalculationService.GetPastThreeCalendarMonths(utcNow);
            var rangeStart = new DateTime(months[0].Year, months[0].Month, 1, 0, 0, 0, DateTimeKind.Utc);
            contributionsByUser = await mutualAidRepository.GetContributionsByMonthForCrewAsync(
                crewId,
                rangeStart,
                rangeStart.AddMonths(3),
                includeLibraryOfThings: true,
                createdBeforeUtc: null,
                cancellationToken);
        }

        var floor = crew?.FinancialMembershipContributionFloor ?? 0m;
        foreach (var member in pending)
        {
            var userLifetime = rawLifetimes.GetValueOrDefault(member.UserId);
            var memberCrewLifetime = crewLifetime;
            if (firstSeason)
            {
                if (seasonMembers.Count == 0)
                {
                    var estimate = member.EstimatedMonthlyContribution ?? 0m;
                    var seeded = Math.Max(userLifetime, estimate);
                    memberCrewLifetime = seeded;
                    userLifetime = seeded;
                }
                else
                {
                    var estimate = member.EstimatedMonthlyContribution
                        ?? seasonMembers.FirstOrDefault(m => m.UserId == member.UserId)?.EstimatedMonthlyContribution
                        ?? 0m;
                    memberCrewLifetime = seededCrewLifetime;
                    userLifetime = Math.Max(userLifetime, estimate);
                }
            }

            if (member.IsHonoraryMember && peerLifetimes is not null)
            {
                var peerValues = peerLifetimes
                    .Where(p => p.Key != member.UserId)
                    .Select(p => p.Value)
                    .ToList();
                if (peerValues.Count > 0)
                {
                    var peerAverage = peerValues.Average();
                    if (peerAverage > userLifetime)
                    {
                        userLifetime = peerAverage;
                    }
                }
            }

            var isFinancialMember = HasPriorityFinancialRole(member, assumeInNeedNonOrganizerForLot);
            if (!isFinancialMember && months is not null && contributionsByUser is not null)
            {
                contributionsByUser.TryGetValue(member.UserId, out var byMonth);
                byMonth ??= new Dictionary<(int Year, int Month), decimal>();
                var average = MutualAidCalculationService.CalculateThreeMonthContributionAverage(
                    months,
                    byMonth,
                    member.GivingSeasonJoinedAt,
                    member.EstimatedMonthlyContribution ?? 0m);
                isFinancialMember = floor <= 0m ? average > 0m : average >= floor;
            }

            scores[member.UserId] = MutualAidCalculationService.CalculatePriorityScore(
                member.User,
                member,
                isFinancialMember,
                memberCrewLifetime,
                userLifetime,
                capacityContext.SurvivalThresholdAmount,
                demoteOrganizerToLastPlace: !assumeInNeedNonOrganizerForLot);
        }

        return scores;
    }

    private static bool HasPriorityFinancialRole(CrewMembership membership, bool assumeInNeedNonOrganizerForLot) =>
        assumeInNeedNonOrganizerForLot
            ? CrewRoleMapper.HasAnyRoleExceptOrganizer(membership)
            : CrewRoleMapper.HasAnyRole(membership);

    private static PriorityScoreBreakdown EmptyPriorityBreakdown() => new(
        Score: 0m,
        CrewLifetimeContributions: 0m,
        EmergencyLevel: 0,
        MembershipBonus: 0m,
        UserLifetimeContributions: 0m,
        SurvivalThresholdAmount: 0m,
        BaseScore: 0m,
        PeopleRepresentedCount: 0,
        DisabilityLevel: 0,
        TargetedMinorityGroupCount: 0,
        PriorityMultiplier: 1,
        PercentBoost: 0,
        SacrificeBonusFactor: 1m,
        IsFinancialMember: false);

    private sealed record PriorityInputs(
        CrewMembership Membership,
        decimal CrewLifetime,
        decimal UserLifetime,
        decimal SurvivalThresholdAmount);
}
