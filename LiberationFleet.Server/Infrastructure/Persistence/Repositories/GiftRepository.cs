using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Services;
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
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
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

    public async Task<GiftLogPage> GetLogPageByCrewIdsAsync(
        IReadOnlyList<int> crewIds,
        int limit,
        DateTime? beforeCreatedAt = null,
        int? beforeId = null,
        CancellationToken cancellationToken = default)
    {
        if (crewIds.Count == 0)
        {
            return new GiftLogPage { Items = Array.Empty<Gift>(), HasMore = false };
        }

        var query = _context.Gifts
            .AsNoTracking()
            .AsSplitQuery()
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
            .Include(g => g.CrewPaymentPlatform)
            .Where(g => crewIds.Contains(g.CrewId));

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
            .Include(g => g.SeasonCycle)
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
        IEnumerable<int>? initiatedGiftIds = null,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts
            .Where(g => g.CrewId == crewId && g.Type == GiftType.Completed && g.InitiatedGiftId != null);

        if (initiatedGiftIds is not null)
        {
            var ids = initiatedGiftIds.Distinct().ToList();
            if (ids.Count == 0)
            {
                return new Dictionary<int, Gift>();
            }

            query = query.Where(g => ids.Contains(g.InitiatedGiftId!.Value));
        }

        var completed = await query.ToListAsync(cancellationToken);

        return completed
            .GroupBy(g => g.InitiatedGiftId!.Value)
            .ToDictionary(
                group => group.Key,
                // Prefer the newest completion if historical duplicates exist.
                group => group.OrderByDescending(g => g.Id).First());
    }

    public async Task<IReadOnlyList<Gift>> GetGiftsByIdsWithUsersAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default)
    {
        var ids = giftIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return Array.Empty<Gift>();
        }

        return await _context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
            .Include(g => g.CrewPaymentPlatform)
            .Where(g => ids.Contains(g.Id))
            .ToListAsync(cancellationToken);
    }

    public async Task AttachPaymentPlatformsToUsersAsync(
        IEnumerable<User> users,
        CancellationToken cancellationToken = default)
    {
        var userList = users
            .Where(u => u is not null)
            .GroupBy(u => u.Id)
            .Select(g => g.First())
            .ToList();
        if (userList.Count == 0)
        {
            return;
        }

        var userIds = userList.Select(u => u.Id).ToList();
        var platforms = await _context.UserPaymentPlatforms
            .Include(p => p.CrewPaymentPlatform)
            .Where(p => userIds.Contains(p.UserId))
            .ToListAsync(cancellationToken);

        var byUser = platforms.GroupBy(p => p.UserId).ToDictionary(g => g.Key, g => (ICollection<UserPaymentPlatform>)g.ToList());
        foreach (var user in userList)
        {
            if (byUser.TryGetValue(user.Id, out var list))
            {
                user.PaymentPlatforms = list;
            }
        }
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
        var yearStart = new DateTime(DateTime.UtcNow.Year, 1, 1, 0, 0, 0, DateTimeKind.Utc);

        var contributedGifts = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.GiverUserId == userId
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        var receivedGifts = _context.Gifts.Where(g =>
            g.CrewId == crewId
            && g.RecipientUserId == userId
            && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        // Sacrifices only count responses to emergency requests, not ordinary gifts.
        var sacrificeQuery = contributedGifts.Where(g => g.EmergencyRequestId != null);
        if (seasonStartDate.HasValue)
        {
            sacrificeQuery = sacrificeQuery.Where(g => g.CreatedAt >= seasonStartDate.Value);
        }
        else
        {
            sacrificeQuery = sacrificeQuery.Where(_ => false);
        }

        var membership = await _context.CrewMemberships
            .FirstOrDefaultAsync(m => m.UserId == userId && m.CrewId == crewId && !m.IsBanned, cancellationToken);
        var months = MutualAidCalculationService.GetPastThreeCalendarMonths(DateTime.UtcNow);
        var rangeStart = new DateTime(months[0].Year, months[0].Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var rangeEnd = rangeStart.AddMonths(3);
        var contributionGifts = await _context.Gifts
            .AsNoTracking()
            .Where(g => g.CrewId == crewId
                && g.GiverUserId == userId
                && g.CountsTowardContribution
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed || g.Type == GiftType.Initiated)
                && g.CreatedAt >= rangeStart
                && g.CreatedAt < rangeEnd
                && (g.CrewPaymentPlatform == null
                    || !g.CrewPaymentPlatform.IsLibraryOfThings))
            .Select(g => new { g.CreatedAt, g.Amount })
            .ToListAsync(cancellationToken);
        var byMonth = contributionGifts
            .GroupBy(g => (g.CreatedAt.Year, g.CreatedAt.Month))
            .ToDictionary(group => group.Key, group => group.Sum(g => g.Amount));
        var averageMonthly = MutualAidCalculationService.CalculateThreeMonthContributionAverage(
            months,
            byMonth,
            membership?.GivingSeasonJoinedAt,
            membership?.EstimatedMonthlyContribution ?? 0m);

        return new CrewmateGiftStatsDto
        {
            SacrificeCountLastSeason = await sacrificeQuery.CountAsync(cancellationToken),
            AverageMonthlyContributions = averageMonthly,
            LifetimeContributions = await contributedGifts.SumAsync(g => g.Amount, cancellationToken),
            ReceptionThisYear = await receivedGifts
                .Where(g => g.CreatedAt >= yearStart)
                .SumAsync(g => g.Amount, cancellationToken)
        };
    }

    public async Task<IReadOnlyList<GiftRecipientSummary>> GetGiverRecipientSummariesAsync(
        int giverUserId,
        CancellationToken cancellationToken = default)
    {
        var grouped = await OutgoingGiftHistoryQuery(giverUserId)
            .GroupBy(g => g.RecipientUserId)
            .Select(g => new
            {
                RecipientUserId = g.Key,
                TotalAmount = g.Sum(x => x.Amount),
                GiftCount = g.Count(),
                LastGiftAt = g.Max(x => x.CreatedAt)
            })
            .ToListAsync(cancellationToken);

        if (grouped.Count == 0)
        {
            return [];
        }

        var recipientIds = grouped.Select(g => g.RecipientUserId).ToList();
        var recipients = await _context.Users
            .Where(u => recipientIds.Contains(u.Id))
            .ToDictionaryAsync(u => u.Id, cancellationToken);

        return grouped
            .Select(g => new GiftRecipientSummary
            {
                RecipientUserId = g.RecipientUserId,
                RecipientUsername = recipients.TryGetValue(g.RecipientUserId, out var recipient)
                    ? GiftDisplayNames.GetRecipientName(recipient)
                    : "Unknown",
                TotalAmount = g.TotalAmount,
                GiftCount = g.GiftCount,
                LastGiftAt = g.LastGiftAt
            })
            .OrderByDescending(g => g.LastGiftAt)
            .ThenBy(g => g.RecipientUsername, StringComparer.OrdinalIgnoreCase)
            .ToList();
    }

    public async Task<IReadOnlyList<Gift>> GetGiftsByGiverAndRecipientAsync(
        int giverUserId,
        int recipientUserId,
        CancellationToken cancellationToken = default)
    {
        return await OutgoingGiftHistoryQuery(giverUserId)
            .Where(g => g.RecipientUserId == recipientUserId)
            .Include(g => g.CrewPaymentPlatform)
            .Include(g => g.MiddlemanUser)
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .ToListAsync(cancellationToken);
    }

    private IQueryable<Gift> OutgoingGiftHistoryQuery(int giverUserId)
    {
        var completedInitiatedIds = _context.Gifts
            .Where(g => g.Type == GiftType.Completed && g.InitiatedGiftId != null)
            .Select(g => g.InitiatedGiftId!.Value);

        return _context.Gifts.Where(g =>
            g.GiverUserId == giverUserId
            && (
                g.Type == GiftType.Direct
                || g.Type == GiftType.Completed
                || (g.Type == GiftType.Initiated && !completedInitiatedIds.Contains(g.Id))));
    }

    public async Task ReassignPlaceholderGiftRecipientsAsync(
        int crewId,
        int fromUserId,
        int toUserId,
        CancellationToken cancellationToken = default)
    {
        var gifts = await _context.Gifts
            .Where(g => g.CrewId == crewId
                && (g.RecipientUserId == fromUserId || g.MiddlemanUserId == fromUserId))
            .ToListAsync(cancellationToken);

        foreach (var gift in gifts)
        {
            if (gift.RecipientUserId == fromUserId)
            {
                gift.RecipientUserId = toUserId;
            }

            if (gift.MiddlemanUserId == fromUserId)
            {
                gift.MiddlemanUserId = toUserId;
            }
        }
    }

    public async Task<IReadOnlyList<PendingReceptionCredit>> GetPendingReceptionCreditsAsync(
        int crewId,
        DateTime? currentSeasonStartDate,
        CancellationToken cancellationToken = default)
    {
        var query = _context.Gifts
            .AsNoTracking()
            .Where(g => g.CrewId == crewId
                && g.CountsTowardReception
                && !g.ReceptionApplied
                && (
                    (g.Type == GiftType.Direct && g.VerificationStatus == GiftVerificationStatus.Pending)
                    || (g.Type == GiftType.Completed
                        && g.VerificationStatus == GiftVerificationStatus.AwaitingRecipientVerification)));

        if (currentSeasonStartDate.HasValue)
        {
            var seasonStart = currentSeasonStartDate.Value;
            query = query.Where(g =>
                (g.SeasonCycleId != null
                    && g.SeasonCycle != null
                    && g.SeasonCycle.SeasonStartDate == seasonStart)
                || (g.SeasonCycleId == null && g.CreatedAt >= seasonStart));
        }

        return await query
            .Select(g => new PendingReceptionCredit
            {
                RecipientUserId = g.RecipientUserId,
                SeasonCycleId = g.SeasonCycleId,
                IsSurvivalThreshold = g.IsSurvivalThreshold,
                IsRepresentativeGift = g.IsRepresentativeGift,
                Amount = g.Amount
            })
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<Gift>> GetGiftsDueForAutoVerificationAsync(
        DateTime createdBeforeUtc,
        int limit = 100,
        CancellationToken cancellationToken = default)
    {
        return await _context.Gifts
            .Include(g => g.GiverUser)
            .Include(g => g.RecipientUser)
            .Include(g => g.MiddlemanUser)
            .Include(g => g.CrewPaymentPlatform)
            .Include(g => g.SeasonCycle)
            .Include(g => g.Crew)
            .Where(g => g.CountsTowardReception
                && !g.ReceptionApplied
                && !g.IsCustomGift
                && g.CreatedAt <= createdBeforeUtc
                && (
                    (g.Type == GiftType.Direct && g.VerificationStatus == GiftVerificationStatus.Pending)
                    || (g.Type == GiftType.Completed
                        && g.VerificationStatus == GiftVerificationStatus.AwaitingRecipientVerification))
                && (g.Crew.CurrentSeasonStartDate == null
                    || (g.SeasonCycle != null && g.SeasonCycle.SeasonStartDate == g.Crew.CurrentSeasonStartDate)
                    || (g.SeasonCycle == null && g.CreatedAt >= g.Crew.CurrentSeasonStartDate)))
            .OrderBy(g => g.CreatedAt)
            .ThenBy(g => g.Id)
            .Take(limit)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GiftComment>> GetCommentsByGiftIdAsync(
        int giftId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GiftComments
            .Include(c => c.AuthorUser)
            .Include(c => c.ReplyToComment!)
                .ThenInclude(r => r.AuthorUser)
            .Where(c => c.GiftId == giftId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public async Task<IReadOnlyList<GiftComment>> GetRepliesByParentCommentIdAsync(
        int giftId,
        int parentCommentId,
        CancellationToken cancellationToken = default)
    {
        return await _context.GiftComments
            .Include(c => c.AuthorUser)
            .Include(c => c.ReplyToComment!)
                .ThenInclude(r => r.AuthorUser)
            .Where(c => c.GiftId == giftId && c.ParentCommentId == parentCommentId && !c.IsDeleted)
            .OrderBy(c => c.CreatedAt)
            .ThenBy(c => c.Id)
            .ToListAsync(cancellationToken);
    }

    public Task<GiftComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default) =>
        _context.GiftComments
            .Include(c => c.AuthorUser)
            .FirstOrDefaultAsync(c => c.Id == commentId && !c.IsDeleted, cancellationToken);

    public async Task<Dictionary<int, int>> GetActiveLikeCountsForGiftsAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default)
    {
        var ids = giftIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.GiftLikes
            .Where(l => l.GiftId.HasValue && ids.Contains(l.GiftId.Value) && l.RemovedAt == null)
            .GroupBy(l => l.GiftId!.Value)
            .Select(g => new { GiftId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GiftId, x => x.Count, cancellationToken);
    }

    public async Task<HashSet<int>> GetActiveLikedGiftIdsByUserAsync(
        int userId,
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default)
    {
        var ids = giftIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var likedIds = await _context.GiftLikes
            .Where(l => l.UserId == userId
                && l.GiftId.HasValue
                && ids.Contains(l.GiftId.Value)
                && l.RemovedAt == null)
            .Select(l => l.GiftId!.Value)
            .ToListAsync(cancellationToken);

        return likedIds.ToHashSet();
    }

    public async Task<Dictionary<int, int>> GetActiveLikeCountsForGiftCommentsAsync(
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.GiftLikes
            .Where(l => l.GiftCommentId.HasValue && ids.Contains(l.GiftCommentId.Value) && l.RemovedAt == null)
            .GroupBy(l => l.GiftCommentId!.Value)
            .Select(g => new { CommentId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.CommentId, x => x.Count, cancellationToken);
    }

    public async Task<HashSet<int>> GetActiveLikedGiftCommentIdsByUserAsync(
        int userId,
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default)
    {
        var ids = commentIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return [];
        }

        var likedIds = await _context.GiftLikes
            .Where(l => l.UserId == userId
                && l.GiftCommentId.HasValue
                && ids.Contains(l.GiftCommentId.Value)
                && l.RemovedAt == null)
            .Select(l => l.GiftCommentId!.Value)
            .ToListAsync(cancellationToken);

        return likedIds.ToHashSet();
    }

    public async Task<Dictionary<int, int>> GetCommentCountsForGiftsAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default)
    {
        var ids = giftIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, int>();
        }

        return await _context.GiftComments
            .Where(c => ids.Contains(c.GiftId) && !c.IsDeleted)
            .GroupBy(c => c.GiftId)
            .Select(g => new { GiftId = g.Key, Count = g.Count() })
            .ToDictionaryAsync(x => x.GiftId, x => x.Count, cancellationToken);
    }

    public Task<GiftLike?> GetGiftLikeAsync(int userId, int giftId, CancellationToken cancellationToken = default) =>
        _context.GiftLikes.FirstOrDefaultAsync(l => l.UserId == userId && l.GiftId == giftId, cancellationToken);

    public Task<GiftLike?> GetGiftCommentLikeAsync(int userId, int commentId, CancellationToken cancellationToken = default) =>
        _context.GiftLikes.FirstOrDefaultAsync(l => l.UserId == userId && l.GiftCommentId == commentId, cancellationToken);

    public async Task AddLikeAsync(GiftLike like, CancellationToken cancellationToken = default) =>
        await _context.GiftLikes.AddAsync(like, cancellationToken);

    public async Task AddCommentAsync(GiftComment comment, CancellationToken cancellationToken = default) =>
        await _context.GiftComments.AddAsync(comment, cancellationToken);

    public async Task<IReadOnlyList<(int UserId, string Username, string? AvatarResourceId)>> GetActiveGiftLikersAsync(
        int giftId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.GiftLikes
            .AsNoTracking()
            .Where(l => l.GiftId == giftId && l.RemovedAt == null)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new { l.UserId, Username = l.User!.Username, l.User.AvatarResourceId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.UserId, r.Username, r.AvatarResourceId)).ToList();
    }

    public async Task<IReadOnlyList<(int UserId, string Username, string? AvatarResourceId)>> GetActiveGiftCommentLikersAsync(
        int commentId,
        CancellationToken cancellationToken = default)
    {
        var rows = await _context.GiftLikes
            .AsNoTracking()
            .Where(l => l.GiftCommentId == commentId && l.RemovedAt == null)
            .OrderBy(l => l.CreatedAt)
            .Select(l => new { l.UserId, Username = l.User!.Username, l.User.AvatarResourceId })
            .ToListAsync(cancellationToken);

        return rows.Select(r => (r.UserId, r.Username, r.AvatarResourceId)).ToList();
    }

    public async Task<Dictionary<int, DateTime?>> GetSeasonStartDatesForGiftsAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default)
    {
        var ids = giftIds.Distinct().ToList();
        if (ids.Count == 0)
        {
            return new Dictionary<int, DateTime?>();
        }

        return await _context.Gifts
            .AsNoTracking()
            .Where(g => ids.Contains(g.Id))
            .Select(g => new
            {
                g.Id,
                SeasonStartDate = g.SeasonCycle != null ? (DateTime?)g.SeasonCycle.SeasonStartDate : null
            })
            .ToDictionaryAsync(x => x.Id, x => x.SeasonStartDate, cancellationToken);
    }

    public async Task<int?> GetPendingCycleThankYouGiftIdAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var latest = await _context.Gifts
            .AsNoTracking()
            .Where(g =>
                g.CrewId == crewId
                && g.Type == GiftType.CycleCompleted
                && g.RecipientUserId == userId)
            .OrderByDescending(g => g.CreatedAt)
            .ThenByDescending(g => g.Id)
            .Select(g => new { g.Id })
            .FirstOrDefaultAsync(cancellationToken);

        if (latest is null)
        {
            return null;
        }

        var hasComment = await _context.GiftComments
            .AsNoTracking()
            .AnyAsync(
                c => c.GiftId == latest.Id && c.AuthorUserId == userId,
                cancellationToken);

        return hasComment ? null : latest.Id;
    }

    public Task EnsureGiftLogSchemaAsync(CancellationToken cancellationToken = default) =>
        GiftLogSchemaRepair.EnsureAsync(_context, cancellationToken: cancellationToken);
}
