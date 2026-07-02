using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure.Persistence.Repositories;

public class LibraryRepository(ApplicationDbContext context) : ILibraryRepository
{
    public async Task<IReadOnlyList<LibraryCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default) =>
        await context.LibraryCategories
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LibraryCategory>> GetCategoriesInUseAsync(
        int crewId,
        LibraryOfferingKind? kind,
        CancellationToken cancellationToken = default)
    {
        var offeringQuery = context.LibraryOfferings
            .AsNoTracking()
            .Where(o => o.CrewId == crewId && !o.IsDeleted);

        if (kind.HasValue)
        {
            offeringQuery = offeringQuery.Where(o => o.Kind == kind.Value);
        }

        offeringQuery = offeringQuery.Where(o => o.Units.Any(u =>
            !u.IsRetired && u.Status != LibraryUnitStatus.Broken));

        var categoryIds = await context.LibraryOfferingCategories
            .AsNoTracking()
            .Where(oc => offeringQuery.Select(o => o.Id).Contains(oc.OfferingId))
            .Select(oc => oc.CategoryId)
            .Distinct()
            .ToListAsync(cancellationToken);

        return await context.LibraryCategories
            .Where(c => categoryIds.Contains(c.Id))
            .OrderBy(c => c.SortOrder)
            .ThenBy(c => c.Name)
            .ToListAsync(cancellationToken);
    }

    public async Task<LibraryUnitListPage> GetDurableUnitsForCrewAsync(
        int crewId,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var query = context.LibraryUnits
            .AsNoTracking()
            .Include(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(u => u.CurrentPossessorUser)
            .Where(u => u.Offering.CrewId == crewId
                && !u.Offering.IsDeleted
                && u.Offering.Kind == LibraryOfferingKind.Durable
                && !u.IsRetired
                && u.Status != LibraryUnitStatus.Broken);

        if (categoryIds.Count > 0)
        {
            query = query.Where(u => u.Offering.Categories.Any(c => categoryIds.Contains(c.CategoryId)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Offering.TitleNormalized.Contains(normalized)
                || u.CurrentPossessorUser.Username.ToLower().Contains(normalized));
        }

        return await ToUnitListPageAsync(query, limit, offset, cancellationToken);
    }

    public async Task<LibraryUnitListPage> GetStockUnitsForCrewAsync(
        int crewId,
        LibraryOfferingKind kind,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var query = context.LibraryUnits
            .AsNoTracking()
            .Include(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(u => u.Offering)
                .ThenInclude(o => o.CreatorUser)
            .Include(u => u.CurrentPossessorUser)
            .Where(u => u.Offering.CrewId == crewId
                && !u.Offering.IsDeleted
                && u.Offering.Kind == kind
                && !u.IsRetired
                && u.Status != LibraryUnitStatus.Broken);

        if (categoryIds.Count > 0)
        {
            query = query.Where(u => u.Offering.Categories.Any(c => categoryIds.Contains(c.CategoryId)));
        }

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(u =>
                u.Offering.TitleNormalized.Contains(normalized)
                || u.Offering.CreatorUser.Username.ToLower().Contains(normalized));
        }

        return await ToUnitListPageAsync(query, limit, offset, cancellationToken);
    }

    public async Task<LibraryOfferingListPage> GetOfferingsByCreatorAsync(
        int crewId,
        int creatorUserId,
        string? search,
        int limit,
        int offset,
        CancellationToken cancellationToken = default)
    {
        var query = context.LibraryOfferings
            .AsNoTracking()
            .Include(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(o => o.CreatorUser)
            .Include(o => o.Units)
            .Where(o => o.CrewId == crewId && o.CreatorUserId == creatorUserId && !o.IsDeleted);

        if (!string.IsNullOrWhiteSpace(search))
        {
            var normalized = search.Trim().ToLowerInvariant();
            query = query.Where(o =>
                o.TitleNormalized.Contains(normalized)
                || o.Categories.Any(c => c.Category.Name.ToLower().Contains(normalized)));
        }

        var ordered = query
            .OrderByDescending(o => o.CreatedAt)
            .ThenByDescending(o => o.Id);

        var fetched = await ordered
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = fetched.Count > limit;
        if (hasMore)
        {
            fetched = fetched.Take(limit).ToList();
        }

        return new LibraryOfferingListPage
        {
            Items = fetched,
            HasMore = hasMore
        };
    }

    public async Task<LibraryOffering?> GetTrackedOfferingByIdAsync(
        int offeringId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryOfferings
            .Include(o => o.Units)
            .FirstOrDefaultAsync(o => o.Id == offeringId, cancellationToken);

    private static async Task<LibraryUnitListPage> ToUnitListPageAsync(
        IQueryable<LibraryUnit> query,
        int limit,
        int offset,
        CancellationToken cancellationToken)
    {
        var ordered = query
            .OrderBy(u => u.Offering.Title)
            .ThenBy(u => u.Id);

        var fetched = await ordered
            .Skip(offset)
            .Take(limit + 1)
            .ToListAsync(cancellationToken);

        var hasMore = fetched.Count > limit;
        if (hasMore)
        {
            fetched = fetched.Take(limit).ToList();
        }

        return new LibraryUnitListPage
        {
            Items = fetched,
            HasMore = hasMore
        };
    }

    public async Task<IReadOnlyList<LibraryCategory>> GetCategoriesByIdsAsync(
        IReadOnlyCollection<int> categoryIds,
        CancellationToken cancellationToken = default) =>
        await context.LibraryCategories
            .Where(c => categoryIds.Contains(c.Id))
            .ToListAsync(cancellationToken);

    public async Task AddOfferingAsync(LibraryOffering offering, CancellationToken cancellationToken = default) =>
        await context.LibraryOfferings.AddAsync(offering, cancellationToken);

    public async Task AddUnitsAsync(IEnumerable<LibraryUnit> units, CancellationToken cancellationToken = default) =>
        await context.LibraryUnits.AddRangeAsync(units, cancellationToken);

    public async Task<LibraryUnit?> GetUnitByIdForCrewAsync(
        int unitId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryUnits
            .AsNoTracking()
            .Include(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(u => u.Offering)
                .ThenInclude(o => o.CreatorUser)
            .Include(u => u.CurrentPossessorUser)
            .FirstOrDefaultAsync(
                u => u.Id == unitId
                    && u.Offering.CrewId == crewId
                    && !u.Offering.IsDeleted,
                cancellationToken);

    public async Task<LibraryUnit?> GetTrackedUnitByIdAsync(
        int unitId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryUnits
            .Include(u => u.Offering)
            .FirstOrDefaultAsync(u => u.Id == unitId, cancellationToken);

    public async Task<LibraryRequest?> GetActiveRequestByUnitAndRequesterAsync(
        int unitId,
        int requesterUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .AsNoTracking()
            .FirstOrDefaultAsync(
                r => r.UnitId == unitId
                    && r.RequesterUserId == requesterUserId
                    && (r.Status == LibraryRequestStatus.Open || r.Status == LibraryRequestStatus.Denied),
                cancellationToken);

    public async Task<LibraryRequest?> GetRequestByIdForCrewAsync(
        int requestId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .AsNoTracking()
            .Include(r => r.RequesterUser)
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(r => r.Unit)
                .ThenInclude(u => u.CurrentPossessorUser)
            .FirstOrDefaultAsync(
                r => r.Id == requestId && r.Unit.Offering.CrewId == crewId,
                cancellationToken);

    public async Task<IReadOnlyList<LibraryRequest>> GetRequestsByRequesterAsync(
        int crewId,
        int requesterUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .AsNoTracking()
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(r => r.Unit)
                .ThenInclude(u => u.CurrentPossessorUser)
            .Where(r => r.RequesterUserId == requesterUserId && r.Unit.Offering.CrewId == crewId)
            .OrderByDescending(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<bool> HasOpenRequestForUnitByUserAsync(
        int unitId,
        int requesterUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests.AnyAsync(
            r => r.UnitId == unitId
                && r.RequesterUserId == requesterUserId
                && r.Status == LibraryRequestStatus.Open,
            cancellationToken);

    public async Task<bool> HasOverlappingOpenRequestForUnitAsync(
        int unitId,
        DateTime neededByStart,
        DateTime neededByEnd,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests.AnyAsync(
            r => r.UnitId == unitId
                && r.Status == LibraryRequestStatus.Open
                && (excludeRequestId == null || r.Id != excludeRequestId)
                && r.NeededByStart <= neededByEnd
                && neededByStart <= r.NeededByEnd,
            cancellationToken);

    public async Task AddRequestAsync(LibraryRequest request, CancellationToken cancellationToken = default) =>
        await context.LibraryRequests.AddAsync(request, cancellationToken);

    public async Task<LibraryRequest?> GetTrackedRequestByIdForRequesterAsync(
        int requestId,
        int requesterUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .FirstOrDefaultAsync(
                r => r.Id == requestId && r.RequesterUserId == requesterUserId,
                cancellationToken);

    public async Task<LibraryRequest?> GetTrackedRequestByIdAsync(
        int requestId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests.FirstOrDefaultAsync(r => r.Id == requestId, cancellationToken);

    public async Task<IReadOnlyList<LibraryRequest>> GetIncomingRequestsForPossessorAsync(
        int crewId,
        int possessorUserId,
        CancellationToken cancellationToken = default)
    {
        var openRequests = await context.LibraryRequests
            .AsNoTracking()
            .Include(r => r.RequesterUser)
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(r => r.Unit)
                .ThenInclude(u => u.CurrentPossessorUser)
            .Where(r => r.Status == LibraryRequestStatus.Open
                && r.Unit.CurrentPossessorUserId == possessorUserId
                && r.Unit.Offering.CrewId == crewId)
            .OrderBy(r => r.NeededByStart)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

        return openRequests
            .GroupBy(r => r.UnitId)
            .Select(g => g.First())
            .OrderBy(r => r.NeededByStart)
            .ThenBy(r => r.CreatedAt)
            .ToList();
    }

    public async Task<IReadOnlyList<LibraryRequest>> GetOpenRequestsForUnitAsync(
        int unitId,
        int crewId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .AsNoTracking()
            .Include(r => r.RequesterUser)
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
                .ThenInclude(o => o.Categories)
                .ThenInclude(c => c.Category)
            .Include(r => r.Unit)
                .ThenInclude(u => u.CurrentPossessorUser)
            .Where(r => r.UnitId == unitId
                && r.Unit.Offering.CrewId == crewId
                && r.Status == LibraryRequestStatus.Open)
            .OrderBy(r => r.NeededByStart)
            .ThenBy(r => r.CreatedAt)
            .ToListAsync(cancellationToken);

    public async Task<int> CountOpenRequestsForUnitAsync(
        int unitId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests.CountAsync(
            r => r.UnitId == unitId && r.Status == LibraryRequestStatus.Open,
            cancellationToken);

    public async Task<LibraryRequest?> GetTrackedRequestByIdForPossessorAsync(
        int requestId,
        int possessorUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
            .FirstOrDefaultAsync(
                r => r.Id == requestId
                    && ((r.Unit.Offering.Kind == LibraryOfferingKind.Consumable
                            || r.Unit.Offering.Kind == LibraryOfferingKind.Service)
                        ? r.Unit.Offering.CreatorUserId == possessorUserId
                        : r.Unit.CurrentPossessorUserId == possessorUserId),
                cancellationToken);

    public async Task<LibraryRequest?> GetTrackedRequestWithUnitForCompleteAsync(
        int requestId,
        int possessorUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
            .Include(r => r.RequesterUser)
            .FirstOrDefaultAsync(
                r => r.Id == requestId
                    && ((r.Unit.Offering.Kind == LibraryOfferingKind.Consumable
                            || r.Unit.Offering.Kind == LibraryOfferingKind.Service)
                        ? r.Unit.Offering.CreatorUserId == possessorUserId
                        : r.Unit.CurrentPossessorUserId == possessorUserId),
                cancellationToken);

    public async Task<IReadOnlyList<LibraryRequestMessage>> GetLatestRequestMessagesAsync(
        int requestId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequestMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
            .Where(m => m.RequestId == requestId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LibraryRequestMessage>> GetRequestMessagesBeforeIdAsync(
        int requestId,
        int beforeMessageId,
        int limit,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequestMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
            .Where(m => m.RequestId == requestId && m.Id < beforeMessageId)
            .OrderByDescending(m => m.CreatedAt)
            .ThenByDescending(m => m.Id)
            .Take(limit)
            .OrderBy(m => m.CreatedAt)
            .ThenBy(m => m.Id)
            .ToListAsync(cancellationToken);

    public async Task AddRequestMessageAsync(LibraryRequestMessage message, CancellationToken cancellationToken = default) =>
        await context.LibraryRequestMessages.AddAsync(message, cancellationToken);

    public async Task<LibraryRequestMessage?> GetRequestMessageByIdWithAuthorAsync(
        int messageId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequestMessages
            .AsNoTracking()
            .Include(m => m.AuthorUser)
            .FirstOrDefaultAsync(m => m.Id == messageId, cancellationToken);

    public async Task<IReadOnlyList<int>> DeleteMessagesForRequestAsync(
        int requestId,
        CancellationToken cancellationToken = default)
    {
        var messageIds = await context.LibraryRequestMessages
            .Where(m => m.RequestId == requestId)
            .Select(m => m.Id)
            .ToListAsync(cancellationToken);

        if (messageIds.Count == 0)
        {
            return messageIds;
        }

        await context.LibraryRequestMessages
            .Where(m => m.RequestId == requestId)
            .ExecuteDeleteAsync(cancellationToken);

        return messageIds;
    }

    public async Task<IReadOnlyList<LibraryRequest>> GetTrackedCancellableRequestsForUnitAsync(
        int unitId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .Where(r => r.UnitId == unitId
                && (r.Status == LibraryRequestStatus.Open || r.Status == LibraryRequestStatus.Denied))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LibraryRequest>> GetTrackedRequestsByRequesterAsync(
        int crewId,
        int requesterUserId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryRequests
            .Include(r => r.Unit)
                .ThenInclude(u => u.Offering)
            .Where(r => r.RequesterUserId == requesterUserId
                && r.Unit.Offering.CrewId == crewId
                && (r.Status == LibraryRequestStatus.Open || r.Status == LibraryRequestStatus.Denied))
            .ToListAsync(cancellationToken);

    public async Task<IReadOnlyList<LibraryUnit>> GetTrackedUnitsPossessedByUserAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default) =>
        await context.LibraryUnits
            .Include(u => u.Offering)
            .Include(u => u.Requests)
            .Where(u => u.CurrentPossessorUserId == userId && u.Offering.CrewId == crewId)
            .ToListAsync(cancellationToken);

    public async Task AddMaintenanceRecordAsync(
        LibraryMaintenanceRecord record,
        CancellationToken cancellationToken = default) =>
        await context.LibraryMaintenanceRecords.AddAsync(record, cancellationToken);

    public async Task CleanupMemberLibraryDataAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default)
    {
        var possessedUnits = await GetTrackedUnitsPossessedByUserAsync(crewId, userId, cancellationToken);
        context.LibraryUnits.RemoveRange(possessedUnits);

        var stockOfferings = await context.LibraryOfferings
            .Where(o => o.CrewId == crewId
                && o.CreatorUserId == userId
                && (o.Kind == LibraryOfferingKind.Consumable || o.Kind == LibraryOfferingKind.Service)
                && !o.IsDeleted)
            .ToListAsync(cancellationToken);

        var utcNow = DateTime.UtcNow;
        foreach (var offering in stockOfferings)
        {
            offering.IsDeleted = true;
            offering.UpdatedAt = utcNow;
        }
    }
}
