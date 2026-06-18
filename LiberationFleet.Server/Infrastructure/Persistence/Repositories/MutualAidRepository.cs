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
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.IsInSeason)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetSeasonReadyMembersAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
            .Where(m => m.CrewId == crewId && !m.IsBanned && m.IsSeasonReady)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<CrewMembership>> GetActiveMembersWithUsersAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.CrewMemberships
            .Include(m => m.User)
            .ThenInclude(u => u.PaymentPlatforms)
            .Where(m => m.CrewId == crewId && !m.IsBanned)
            .ToListAsync(cancellationToken);

    public Task<SeasonCycle?> GetSeasonCycleAsync(int crewId, int userId, DateTime seasonStartDate, CancellationToken cancellationToken = default) =>
        _context.SeasonCycles.FirstOrDefaultAsync(
            c => c.CrewId == crewId && c.UserId == userId && c.SeasonStartDate == seasonStartDate,
            cancellationToken);

    public async Task<IReadOnlyList<SeasonCycle>> GetSeasonCyclesAsync(int crewId, DateTime seasonStartDate, CancellationToken cancellationToken = default) =>
        await _context.SeasonCycles
            .Include(c => c.User)
            .Where(c => c.CrewId == crewId && c.SeasonStartDate == seasonStartDate)
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

    public async Task<decimal> GetContributionsLast3MonthsAsync(int userId, int crewId, CancellationToken cancellationToken = default)
    {
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
        return await _context.Gifts
            .Where(g => g.CrewId == crewId
                && g.GiverUserId == userId
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated)
                && g.CreatedAt >= threeMonthsAgo)
            .SumAsync(g => g.Amount, cancellationToken);
    }

    public async Task<decimal> GetLifetimeContributionsAsync(
        int userId,
        int crewId,
        DateTime? before = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.GiverUserId == userId
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
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated));

        if (before.HasValue)
        {
            query = query.Where(g => g.CreatedAt < before.Value);
        }

        return await query.SumAsync(g => g.Amount, cancellationToken);
    }

    public async Task<bool> HasContributedSinceAsync(int userId, int crewId, DateTime since, DateTime? until = null, CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.GiverUserId == userId
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

        return dates.FirstOrDefault();
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
}
