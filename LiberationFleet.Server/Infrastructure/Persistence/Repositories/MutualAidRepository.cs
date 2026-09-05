using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class MutualAidRepository : IMutualAidRepository
{
    private readonly ApplicationDbContext _context;

    public MutualAidRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public Task<Crew?> GetCrewAsync(int crewId, CancellationToken cancellationToken = default) =>
        _context.Crews.FirstOrDefaultAsync(c => c.Id == crewId, cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetSeasonParticipantsAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
                .ThenInclude(p => p.CrewPaymentPlatform)
            .AsSplitQuery()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.IsInSeason)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetSeasonReadyMembersAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
                .ThenInclude(p => p.CrewPaymentPlatform)
            .AsSplitQuery()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.IsSeasonReady)
            .ToListAsync(cancellationToken);

    public Task<int> CountSeasonReadyMembersAsync(int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships
            .AsNoTracking()
            .CountAsync(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.IsSeasonReady, cancellationToken);

    public Task<int> CountSeasonParticipantsNeedingSurvivalAidAsync(int crewId, CancellationToken cancellationToken = default) =>
        _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.IsInSeason && m.User.NeedsSurvivalAid)
            .CountAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetActiveMembersWithUsersAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
                .ThenInclude(p => p.CrewPaymentPlatform)
            .AsSplitQuery()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null)
            .ToListAsync(cancellationToken);

    public Task<CrewMembership?> GetMembershipWithUserAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        _context.CrewMemberships
            .AsNoTracking()
            .Include(m => m.User)
            .FirstOrDefaultAsync(
                m => m.UserId == userId && m.CrewId == crewId && !m.IsBanned && m.LeftAt == null,
                cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetSeasonContributionMembersAsync(
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var participants = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.IsInSeason)
            .ToListAsync(cancellationToken);

        if (participants.Count > 0)
        {
            return participants;
        }

        return await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.IsSeasonReady)
            .ToListAsync(cancellationToken);
    }

    public Task<SeasonCycle?> GetSeasonCycleAsync(int crewId, int userId, DateTime seasonStartDate, CancellationToken cancellationToken = default) =>
        _context.SeasonCycles.FirstOrDefaultAsync(
            c => c.CrewId == crewId && c.UserId == userId && c.SeasonStartDate == seasonStartDate,
            cancellationToken);

    public Task<SeasonCycle?> GetPrimarySeasonCycleAsync(
        int crewId,
        int userId,
        DateTime seasonStartDate,
        CancellationToken cancellationToken = default) =>
        _context.SeasonCycles
            .Where(c =>
                c.CrewId == crewId
                && c.UserId == userId
                && c.SeasonStartDate == seasonStartDate
                && c.EmergencyRequestId == null
                && c.EmergencySplitOfferId == null)
            .OrderBy(c => c.ReceptionOrderPosition)
            .FirstOrDefaultAsync(cancellationToken);

    public Task<SeasonCycle?> GetSeasonCycleByIdAsync(int cycleId, CancellationToken cancellationToken = default) =>
        _context.SeasonCycles
            .Include(c => c.User)
            .FirstOrDefaultAsync(c => c.Id == cycleId, cancellationToken);

    public async Task<IReadOnlyList<SeasonCycle>> GetSeasonCyclesAsync(int crewId, DateTime seasonStartDate, CancellationToken cancellationToken = default) =>
        await _context.SeasonCycles
            .Include(c => c.User)
            .Where(c => c.CrewId == crewId && c.SeasonStartDate == seasonStartDate)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<DateTime>> GetSeasonStartDatesOnOrAfterAsync(
        int crewId,
        DateTime onOrAfter,
        CancellationToken cancellationToken = default) =>
        await _context.SeasonCycles
            .Where(c => c.CrewId == crewId && c.SeasonStartDate >= onOrAfter)
            .Select(c => c.SeasonStartDate)
            .Distinct()
            .OrderBy(d => d)
            .ToListAsync(cancellationToken);

    public async Task AddSeasonCycleAsync(SeasonCycle cycle, CancellationToken cancellationToken = default)
    {
        await _context.SeasonCycles.AddAsync(cycle, cancellationToken);
    }

    public async Task ClearSeasonDataAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var giftsWithCycle = await _context.Gifts
            .Where(g => g.CrewId == crewId && g.SeasonCycleId != null)
            .ToListAsync(cancellationToken);
        foreach (var gift in giftsWithCycle)
        {
            gift.SeasonCycleId = null;
        }

        // Emergency split offers Restrict-FK to season cycles; wipe crew emergencies with the season.
        var emergencyRequestIds = await _context.EmergencyRequests
            .Where(r => r.CrewId == crewId)
            .Select(r => r.Id)
            .ToListAsync(cancellationToken);
        if (emergencyRequestIds.Count > 0)
        {
            var responses = await _context.EmergencyGiftResponses
                .Where(r => emergencyRequestIds.Contains(r.EmergencyRequestId))
                .ToListAsync(cancellationToken);
            _context.EmergencyGiftResponses.RemoveRange(responses);

            var offers = await _context.EmergencySplitOffers
                .Where(o => emergencyRequestIds.Contains(o.EmergencyRequestId))
                .ToListAsync(cancellationToken);
            _context.EmergencySplitOffers.RemoveRange(offers);

            var requests = await _context.EmergencyRequests
                .Where(r => r.CrewId == crewId)
                .ToListAsync(cancellationToken);
            _context.EmergencyRequests.RemoveRange(requests);
        }

        var cycles = await _context.SeasonCycles
            .Where(c => c.CrewId == crewId)
            .ToListAsync(cancellationToken);
        _context.SeasonCycles.RemoveRange(cycles);

        var thresholds = await _context.MonthlySurvivalThresholds
            .Where(t => t.CrewId == crewId)
            .ToListAsync(cancellationToken);
        _context.MonthlySurvivalThresholds.RemoveRange(thresholds);
    }

    public async Task<IReadOnlyList<MonthlySurvivalThreshold>> GetUnsatisfiedThresholdsAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.MonthlySurvivalThresholds
            .Include(t => t.User)
            .Where(t => t.CrewId == crewId && !t.Satisfied)
            .OrderBy(t => t.Year)
            .ThenBy(t => t.Month)
            .ThenBy(t => t.ReceptionOrderPosition)
            .ToListAsync(cancellationToken);

    public Task<MonthlySurvivalThreshold?> GetThresholdByIdAsync(int thresholdId, CancellationToken cancellationToken = default) =>
        _context.MonthlySurvivalThresholds.FirstOrDefaultAsync(t => t.Id == thresholdId, cancellationToken);

    public async Task AddThresholdAsync(MonthlySurvivalThreshold threshold, CancellationToken cancellationToken = default)
    {
        await _context.MonthlySurvivalThresholds.AddAsync(threshold, cancellationToken);
    }

    public Task<bool> HasThresholdForMonthAsync(int crewId, int userId, int year, int month, CancellationToken cancellationToken = default) =>
        _context.MonthlySurvivalThresholds.AnyAsync(
            t => t.CrewId == crewId && t.UserId == userId && t.Year == year && t.Month == month,
            cancellationToken);

    public Task<IReadOnlyDictionary<(int Year, int Month), decimal>> GetFinancialContributionsByMonthAsync(
        int userId,
        int crewId,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        CancellationToken cancellationToken = default) =>
        GetContributionsByMonthAsync(
            userId,
            crewId,
            rangeStartUtc,
            rangeEndExclusiveUtc,
            includeLibraryOfThings: false,
            createdBeforeUtc: null,
            cancellationToken);

    public async Task<IReadOnlyDictionary<(int Year, int Month), decimal>> GetContributionsByMonthAsync(
        int userId,
        int crewId,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        bool includeLibraryOfThings,
        DateTime? createdBeforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts
            .AsNoTracking()
            .Where(g => g.CrewId == crewId
                && g.GiverUserId == userId
                && g.CountsTowardContribution
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated)
                && g.CreatedAt >= rangeStartUtc
                && g.CreatedAt < rangeEndExclusiveUtc);

        if (createdBeforeUtc.HasValue)
        {
            query = query.Where(g => g.CreatedAt < createdBeforeUtc.Value);
        }

        if (!includeLibraryOfThings)
        {
            query = query.Where(g =>
                g.CrewPaymentPlatform == null
                || !g.CrewPaymentPlatform.IsLibraryOfThings);
        }

        var gifts = await query
            .Select(g => new { g.CreatedAt, g.Amount })
            .ToListAsync(cancellationToken);

        return gifts
            .GroupBy(g => (g.CreatedAt.Year, g.CreatedAt.Month))
            .ToDictionary(group => group.Key, group => group.Sum(g => g.Amount));
    }

    public async Task<IReadOnlyDictionary<int, IReadOnlyDictionary<(int Year, int Month), decimal>>> GetContributionsByMonthForCrewAsync(
        int crewId,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        bool includeLibraryOfThings,
        DateTime? createdBeforeUtc = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts
            .AsNoTracking()
            .Where(g => g.CrewId == crewId
                && g.CountsTowardContribution
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated)
                && g.CreatedAt >= rangeStartUtc
                && g.CreatedAt < rangeEndExclusiveUtc);

        if (createdBeforeUtc.HasValue)
        {
            query = query.Where(g => g.CreatedAt < createdBeforeUtc.Value);
        }

        if (!includeLibraryOfThings)
        {
            query = query.Where(g =>
                g.CrewPaymentPlatform == null
                || !g.CrewPaymentPlatform.IsLibraryOfThings);
        }

        var gifts = await query
            .Select(g => new { g.GiverUserId, g.CreatedAt, g.Amount })
            .ToListAsync(cancellationToken);

        return gifts
            .GroupBy(g => g.GiverUserId)
            .ToDictionary(
                group => group.Key,
                group => (IReadOnlyDictionary<(int Year, int Month), decimal>)group
                    .GroupBy(g => (g.CreatedAt.Year, g.CreatedAt.Month))
                    .ToDictionary(month => month.Key, month => month.Sum(g => g.Amount)));
    }

    public async Task<decimal> GetLifetimeContributionsAsync(
        int userId,
        int crewId,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        var totals = await GetLifetimeContributionsForUsersAsync(
            crewId,
            [userId],
            before,
            cancellationToken);
        return totals.GetValueOrDefault(userId);
    }

    public async Task<IReadOnlyDictionary<int, decimal>> GetLifetimeContributionsForUsersAsync(
        int crewId,
        IReadOnlyCollection<int> userIds,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        if (userIds.Count == 0)
        {
            return new Dictionary<int, decimal>();
        }

        var ids = userIds as IList<int> ?? userIds.ToList();
        var overrides = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && ids.Contains(m.UserId) && m.LifetimeContributionOverride != null)
            .Select(m => new { m.UserId, Override = m.LifetimeContributionOverride!.Value })
            .ToListAsync(cancellationToken);

        var overrideByUser = overrides.ToDictionary(o => o.UserId, o => o.Override);
        var usersNeedingGiftSum = ids.Where(id => !overrideByUser.ContainsKey(id)).ToList();

        var result = new Dictionary<int, decimal>(ids.Count);
        foreach (var entry in overrideByUser)
        {
            result[entry.Key] = entry.Value;
        }

        if (usersNeedingGiftSum.Count == 0)
        {
            return result;
        }

        var giftQuery = _context.Gifts
            .AsNoTracking()
            .Where(g =>
                g.CrewId == crewId
                && usersNeedingGiftSum.Contains(g.GiverUserId)
                && g.CountsTowardContribution
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated));

        if (before.HasValue)
        {
            giftQuery = giftQuery.Where(g => g.CreatedAt < before.Value);
        }

        var giftTotals = await giftQuery
            .GroupBy(g => g.GiverUserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        foreach (var userId in usersNeedingGiftSum)
        {
            result[userId] = 0m;
        }

        foreach (var entry in giftTotals)
        {
            result[entry.UserId] = entry.Total;
        }

        return result;
    }

    public async Task<decimal> GetCrewLifetimeContributionsAsync(
        int crewId,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        var giftQuery = _context.Gifts
            .AsNoTracking()
            .Where(g =>
                g.CrewId == crewId
                && g.CountsTowardContribution
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated));

        if (before.HasValue)
        {
            giftQuery = giftQuery.Where(g => g.CreatedAt < before.Value);
        }

        var giftTotalsByUser = await giftQuery
            .GroupBy(g => g.GiverUserId)
            .Select(g => new { UserId = g.Key, Total = g.Sum(x => x.Amount) })
            .ToListAsync(cancellationToken);

        var giftTotal = giftTotalsByUser.Sum(g => g.Total);
        var overrides = await _context.CrewMemberships
            .AsNoTracking()
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LeftAt == null && m.LifetimeContributionOverride != null)
            .Select(m => new { m.UserId, Override = m.LifetimeContributionOverride!.Value })
            .ToListAsync(cancellationToken);

        if (overrides.Count == 0)
        {
            return giftTotal;
        }

        var giftByUser = giftTotalsByUser.ToDictionary(g => g.UserId, g => g.Total);
        foreach (var entry in overrides)
        {
            var userGiftTotal = giftByUser.GetValueOrDefault(entry.UserId);
            giftTotal = giftTotal - userGiftTotal + entry.Override;
        }

        return giftTotal;
    }

    public async Task<bool> HasContributedSinceAsync(int userId, int crewId, DateTime since, DateTime? until = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.GiverUserId == userId
            && g.CountsTowardContribution
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated)
            && g.CreatedAt >= since);

        if (until.HasValue)
        {
            query = query.Where(g => g.CreatedAt < until.Value);
        }

        return await query.AnyAsync(cancellationToken);
    }

    public async Task<DateTime?> GetPreviousSeasonStartDateAsync(int crewId, DateTime currentSeasonStart, CancellationToken cancellationToken = default)
    {
        var dates = await _context.SeasonCycles
            .Where(c => c.CrewId == crewId && c.SeasonStartDate < currentSeasonStart)
            .Select(c => c.SeasonStartDate)
            .Distinct()
            .OrderByDescending(d => d)
            .ToListAsync(cancellationToken);

        return dates.Count == 0 ? null : dates[0];
    }

    public async Task<int> GetNextThresholdOrderPositionAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var maxPosition = await _context.MonthlySurvivalThresholds
            .Where(t => t.CrewId == crewId && !t.Satisfied)
            .Select(t => (int?)t.ReceptionOrderPosition)
            .MaxAsync(cancellationToken);

        return (maxPosition ?? -1) + 1;
    }

    public async Task<(int Year, int Month)?> GetLatestThresholdMonthAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var latest = await _context.MonthlySurvivalThresholds
            .Where(t => t.CrewId == crewId)
            .OrderByDescending(t => t.Year)
            .ThenByDescending(t => t.Month)
            .Select(t => new { t.Year, t.Month })
            .FirstOrDefaultAsync(cancellationToken);

        return latest is null ? null : (latest.Year, latest.Month);
    }

    public async Task MergePlaceholderIdentityDataAsync(
        int crewId,
        int placeholderUserId,
        int claimantUserId,
        CancellationToken cancellationToken = default)
    {
        var placeholderCycles = await _context.SeasonCycles
            .Where(c => c.CrewId == crewId && c.UserId == placeholderUserId)
            .ToListAsync(cancellationToken);

        if (placeholderCycles.Count > 0)
        {
            var seasonDates = placeholderCycles.Select(c => c.SeasonStartDate).Distinct().ToList();
            var claimantCycles = await _context.SeasonCycles
                .Where(c => c.CrewId == crewId && c.UserId == claimantUserId && seasonDates.Contains(c.SeasonStartDate))
                .ToListAsync(cancellationToken);

            foreach (var placeholderCycle in placeholderCycles)
            {
                var isPrimary = placeholderCycle.EmergencyRequestId is null
                    && placeholderCycle.EmergencySplitOfferId is null;

                if (isPrimary)
                {
                    var claimantPrimary = claimantCycles.FirstOrDefault(c =>
                        c.SeasonStartDate == placeholderCycle.SeasonStartDate
                        && c.EmergencyRequestId is null
                        && c.EmergencySplitOfferId is null);

                    if (claimantPrimary is not null)
                    {
                        claimantPrimary.TotalReceptionAmount += placeholderCycle.TotalReceptionAmount;
                        claimantPrimary.SurvivalThresholdReceived += placeholderCycle.SurvivalThresholdReceived;
                        claimantPrimary.CycleReceived += placeholderCycle.CycleReceived;
                        claimantPrimary.CycleCompleted = claimantPrimary.CycleCompleted || placeholderCycle.CycleCompleted;
                        claimantPrimary.HasCycleStarted = claimantPrimary.HasCycleStarted || placeholderCycle.HasCycleStarted;
                        _context.SeasonCycles.Remove(placeholderCycle);
                        continue;
                    }
                }

                placeholderCycle.UserId = claimantUserId;
                claimantCycles.Add(placeholderCycle);
            }
        }

        var thresholds = await _context.MonthlySurvivalThresholds
            .Where(t => t.CrewId == crewId && t.UserId == placeholderUserId)
            .ToListAsync(cancellationToken);

        if (thresholds.Count == 0)
        {
            return;
        }

        var claimantThresholds = await _context.MonthlySurvivalThresholds
            .Where(t => t.CrewId == crewId && t.UserId == claimantUserId)
            .ToListAsync(cancellationToken);
        var claimantByMonth = claimantThresholds
            .ToDictionary(t => (t.Year, t.Month));

        foreach (var threshold in thresholds)
        {
            if (claimantByMonth.TryGetValue((threshold.Year, threshold.Month), out var claimantThreshold))
            {
                claimantThreshold.ReceivedAmount += threshold.ReceivedAmount;
                claimantThreshold.Satisfied = claimantThreshold.ReceivedAmount >= claimantThreshold.ThresholdAmount;
                _context.MonthlySurvivalThresholds.Remove(threshold);
            }
            else
            {
                threshold.UserId = claimantUserId;
                claimantByMonth[(threshold.Year, threshold.Month)] = threshold;
            }
        }
    }
}
