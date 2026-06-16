using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
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

    public async Task<IReadOnlyList<Gift>> GetLogByCrewIdAsync(int crewId, CancellationToken cancellationToken = default) =>
        await _context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
            .Include(g => g.PaymentPlatform)
            .Where(g => g.CrewId == crewId)
            .OrderBy(g => g.CreatedAt)
            .ToListAsync(cancellationToken);

    public Task<Gift?> GetByIdWithUsersAsync(int id, CancellationToken cancellationToken = default) =>
        _context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
            .Include(g => g.PaymentPlatform)
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
            .Include(g => g.PaymentPlatform)
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
}
