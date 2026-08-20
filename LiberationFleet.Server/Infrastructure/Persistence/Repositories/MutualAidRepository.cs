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
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.IsInSeason)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetSeasonReadyMembersAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
                .ThenInclude(p => p.CrewPaymentPlatform)
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.IsSeasonReady)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetActiveMembersWithUsersAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
                .ThenInclude(p => p.CrewPaymentPlatform)
            .Where(m => m.CrewId == crewId && !m.IsBanned)
            .ToListAsync(cancellationToken);

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
        const string libraryOfThingsPlatformName = "Library of Things";
        var query = _context.Gifts
            .Include(g => g.CrewPaymentPlatform)
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
                || g.CrewPaymentPlatform.Name != libraryOfThingsPlatformName);
        }

        var gifts = await query
            .Select(g => new { g.CreatedAt, g.Amount })
            .ToListAsync(cancellationToken);

        return gifts
            .GroupBy(g => (g.CreatedAt.Year, g.CreatedAt.Month))
            .ToDictionary(group => group.Key, group => group.Sum(g => g.Amount));
    }

    public async Task<decimal> GetLifetimeContributionsAsync(
        int userId,
        int crewId,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        var overrideAmount = await _context.CrewMemberships
            .Where(m => m.CrewId == crewId && m.UserId == userId && !m.IsBanned)
            .Select(m => m.LifetimeContributionOverride)
            .FirstOrDefaultAsync(cancellationToken);
        if (overrideAmount.HasValue)
        {
            return overrideAmount.Value;
        }

        var query = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.GiverUserId == userId
            && g.CountsTowardContribution
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated));

        if (before.HasValue)
        {
            query = query.Where(g => g.CreatedAt < before.Value);
        }

        return await query.SumAsync(g => g.Amount, cancellationToken);
    }

    public async Task<decimal> GetCrewLifetimeContributionsAsync(
        int crewId,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.CountsTowardContribution
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated));

        if (before.HasValue)
        {
            query = query.Where(g => g.CreatedAt < before.Value);
        }

        var giftTotal = await query.SumAsync(g => g.Amount, cancellationToken);
        var overrides = await _context.CrewMemberships
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.LifetimeContributionOverride != null)
            .Select(m => new { m.UserId, Override = m.LifetimeContributionOverride!.Value })
            .ToListAsync(cancellationToken);

        if (overrides.Count == 0)
        {
            return giftTotal;
        }

        foreach (var entry in overrides)
        {
            var userGiftQuery = _context.Gifts.Where(g =>
                g.CrewId == crewId
                && g.GiverUserId == entry.UserId
                && g.CountsTowardContribution
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated));

            if (before.HasValue)
            {
                userGiftQuery = userGiftQuery.Where(g => g.CreatedAt < before.Value);
            }

            var userGiftTotal = await userGiftQuery.SumAsync(g => g.Amount, cancellationToken);
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

        foreach (var threshold in thresholds)
        {
            var claimantThreshold = await _context.MonthlySurvivalThresholds
                .FirstOrDefaultAsync(
                    t => t.CrewId == crewId
                        && t.UserId == claimantUserId
                        && t.Year == threshold.Year
                        && t.Month == threshold.Month,
                    cancellationToken);

            if (claimantThreshold is not null)
            {
                claimantThreshold.ReceivedAmount += threshold.ReceivedAmount;
                claimantThreshold.Satisfied = claimantThreshold.ReceivedAmount >= claimantThreshold.ThresholdAmount;
                _context.MonthlySurvivalThresholds.Remove(threshold);
            }
            else
            {
                threshold.UserId = claimantUserId;
            }
        }
    }
}
