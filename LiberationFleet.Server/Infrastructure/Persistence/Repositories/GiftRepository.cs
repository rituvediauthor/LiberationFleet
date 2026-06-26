using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class GiftRepository : IGiftRepository
{
    private readonly ApplicationDbContext _context;

    public GiftRepository(ApplicationDbContext context)
    {
        _context = context;
    }

    public async Task<GiftLogPage> GetLogPageByCrewIdAsync(
        int crewId,
        int limit,
        DateTime? beforeCreatedAt = null,
        int? beforeId = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts
            .Include(g => g.GiverUser)
                .ThenInclude(u => u.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(g => g.RecipientUser)
                .ThenInclude(u => u.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(g => g.MiddlemanUser)
                .ThenInclude(u => u!.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(g => g.CrewPaymentPlatform)
            .Where(g => g.CrewId == crewId);

        if (beforeCreatedAt.HasValue && beforeId.HasValue)
        {
            query = query.Where(g =>
                g.CreatedAt < beforeCreatedAt.Value
                || (g.CreatedAt == beforeCreatedAt.Value && g.Id < beforeId.Value));
        }

        var fetched = await query
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = fetched.Count > limit;
        if (hasMore)
        {
            fetched = fetched.Take(limit).ToList();
        }

        fetched.Reverse();

        return new GiftLogPage
        {
            Items = fetched,
            HasMore = hasMore
        };
    }

    public async Task<IReadOnlySet<int>> GetCompletedInitiatedGiftIdsAsync(int crewId, CancellationToken cancellationToken = default)
    {
        var ids = await _context.Gifts
            .Where(g => g.CrewId == crewId && g.Type == GiftType.Completed && g.InitiatedGiftId != null)
            .Select(g => g.InitiatedGiftId!.Value)
            .ToListAsync(cancellationToken);

        return ids.ToHashSet();
    }

    public Task<Gift?> GetByIdWithUsersAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Gifts
            .Include(g => g.GiverUser)
                .ThenInclude(u => u.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(g => g.RecipientUser)
                .ThenInclude(u => u.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(g => g.MiddlemanUser)
                .ThenInclude(u => u!.PaymentPlatforms)
                    .ThenInclude(p => p.CrewPaymentPlatform)
            .Include(g => g.CrewPaymentPlatform)
            .FirstOrDefaultAsync(g => g.Id == id, cancellationToken);

    public async Task<IReadOnlyList<Gift>> GetPendingMiddlemanGiftsAsync(
        int middlemanUserId,
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var completedInitiatedIds = await _context.Gifts
            .Where(g => g.CrewId == crewId && g.Type == GiftType.Completed && g.InitiatedGiftId != null)
            .Select(g => g.InitiatedGiftId!.Value)
            .ToListAsync(cancellationToken);

        return await _context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.CrewPaymentPlatform)
            .Where(g => g.CrewId == crewId
                && g.Type == GiftType.Initiated
                && g.MiddlemanUserId == middlemanUserId
                && !completedInitiatedIds.Contains(g.Id))
            .OrderByDescending(g => g.CreatedAt)
            .ToListAsync(cancellationToken);
    }

    public Task<bool> HasCompletedInitiatedGiftAsync(int initiatedGiftId, CancellationToken cancellationToken = default) =>
        _context.Gifts.AnyAsync(
            g => g.InitiatedGiftId == initiatedGiftId && g.Type == GiftType.Completed,
            cancellationToken);

    public Task<Gift?> GetCompletedGiftForInitiatedAsync(int initiatedGiftId, CancellationToken cancellationToken = default) =>
        _context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
            .Include(g => g.CrewPaymentPlatform)
            .FirstOrDefaultAsync(
                g => g.InitiatedGiftId == initiatedGiftId && g.Type == GiftType.Completed,
                cancellationToken);

    public async Task<IReadOnlyDictionary<int, Gift>> GetCompletedGiftsByInitiatedIdsAsync(
        int crewId,
        CancellationToken cancellationToken = default)
    {
        var completed = await _context.Gifts
            .Where(g => g.CrewId == crewId && g.Type == GiftType.Completed && g.InitiatedGiftId != null)
            .ToListAsync(cancellationToken);

        return completed.ToDictionary(g => g.InitiatedGiftId!.Value);
    }

    public async Task AddAsync(Gift gift, CancellationToken cancellationToken = default)
    {
        await _context.Gifts.AddAsync(gift, cancellationToken);
    }

    public async Task<UserGiftStats> GetUserGiftStatsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var oneYearAgo = DateTime.UtcNow.AddYears(-1);
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);

        var contributedGifts = _context.Gifts.Where(g =>
            g.GiverUserId == userId
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        var receivedGifts = _context.Gifts.Where(g =>
            g.RecipientUserId == userId
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        return new UserGiftStats
        {
            LifetimeContributions = await contributedGifts.SumAsync(g => g.Amount, cancellationToken),
            SacrificeCountLastYear = await contributedGifts
                .Where(g => g.CreatedAt >= oneYearAgo)
                .CountAsync(cancellationToken),
            ContributionsLast3Months = await contributedGifts
                .Where(g => g.CreatedAt >= threeMonthsAgo)
                .SumAsync(g => g.Amount, cancellationToken),
            ReceptionLastYear = await receivedGifts
                .Where(g => g.CreatedAt >= oneYearAgo)
                .SumAsync(g => g.Amount, cancellationToken)
        };
    }

    public async Task<CrewmateGiftStatsDto> GetCrewmateGiftStatsAsync(
        int userId,
        int crewId,
        DateTime? seasonStartDate,
        CancellationToken cancellationToken = default)
    {
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);
        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var contributedGifts = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.GiverUserId == userId
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        var receivedGifts = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.RecipientUserId == userId
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        var sacrificeQuery = contributedGifts;
        if (seasonStartDate.HasValue)
        {
            sacrificeQuery = sacrificeQuery.Where(g => g.CreatedAt >= seasonStartDate.Value);
        }
        else
        {
            sacrificeQuery = sacrificeQuery.Where(_ => false);
        }

        var contributionsLast3Months = await contributedGifts
            .Where(g => g.CreatedAt >= threeMonthsAgo)
            .SumAsync(g => g.Amount, cancellationToken);

        return new CrewmateGiftStatsDto
        {
            SacrificeCountLastSeason = await sacrificeQuery.CountAsync(cancellationToken),
            AverageMonthlyContributions = Math.Round(contributionsLast3Months / 3m, 2),
            LifetimeContributions = await contributedGifts.SumAsync(g => g.Amount, cancellationToken),
            ReceptionThisYear = await receivedGifts
                .Where(g => g.CreatedAt >= yearStart)
                .SumAsync(g => g.Amount, cancellationToken)
        };
    }
}
