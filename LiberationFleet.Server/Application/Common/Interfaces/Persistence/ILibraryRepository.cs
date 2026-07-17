using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface ILibraryRepository
{
    Task<IReadOnlyList<LibraryCategory>> GetCategoriesAsync(CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryCategory>> GetCategoriesInUseAsync(
        int crewId,
        LibraryOfferingKind? kind,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryCategory>> GetCategoriesInUseForCrewIdsAsync(
        IReadOnlyCollection<int> crewIds,
        LibraryOfferingKind? kind,
        CancellationToken cancellationToken = default);

    Task<LibraryUnitListPage> GetDurableUnitsForCrewAsync(
        int crewId,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<LibraryUnitListPage> GetDurableUnitsForCrewIdsAsync(
        IReadOnlyCollection<int> crewIds,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<LibraryUnitListPage> GetStockUnitsForCrewAsync(
        int crewId,
        LibraryOfferingKind kind,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<LibraryUnitListPage> GetStockUnitsForCrewIdsAsync(
        IReadOnlyCollection<int> crewIds,
        LibraryOfferingKind kind,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<LibraryOfferingListPage> GetOfferingsByCreatorAsync(
        int crewId,
        int creatorUserId,
        string? search,
        IReadOnlyCollection<int> categoryIds,
        int limit,
        int offset,
        CancellationToken cancellationToken = default);

    Task<LibraryOffering?> GetTrackedOfferingByIdAsync(
        int offeringId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryCategory>> GetCategoriesByIdsAsync(
        IReadOnlyCollection<int> categoryIds,
        CancellationToken cancellationToken = default);

    Task AddOfferingAsync(LibraryOffering offering, CancellationToken cancellationToken = default);

    Task AddUnitsAsync(IEnumerable<LibraryUnit> units, CancellationToken cancellationToken = default);

    Task<LibraryUnit?> GetUnitByIdForCrewAsync(
        int unitId,
        int crewId,
        CancellationToken cancellationToken = default);

    Task<LibraryUnit?> GetUnitByIdForCrewIdsAsync(
        int unitId,
        IReadOnlyCollection<int> crewIds,
        CancellationToken cancellationToken = default);

    Task<LibraryUnit?> GetTrackedUnitByIdAsync(
        int unitId,
        CancellationToken cancellationToken = default);

    Task<LibraryRequest?> GetActiveRequestByUnitAndRequesterAsync(
        int unitId,
        int requesterUserId,
        CancellationToken cancellationToken = default);

    Task<LibraryRequest?> GetRequestByIdForCrewAsync(
        int requestId,
        int crewId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequest>> GetRequestsByRequesterAsync(
        int crewId,
        int requesterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequest>> GetIncomingRequestsForPossessorAsync(
        int crewId,
        int possessorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequest>> GetOpenRequestsForUnitAsync(
        int unitId,
        int crewId,
        CancellationToken cancellationToken = default);

    Task<int> CountOpenRequestsForUnitAsync(
        int unitId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOpenRequestForUnitByUserAsync(
        int unitId,
        int requesterUserId,
        CancellationToken cancellationToken = default);

    Task<bool> HasOverlappingOpenRequestForUnitAsync(
        int unitId,
        DateTime neededByStart,
        DateTime neededByEnd,
        int? excludeRequestId = null,
        CancellationToken cancellationToken = default);

    Task AddRequestAsync(LibraryRequest request, CancellationToken cancellationToken = default);

    Task<LibraryRequest?> GetTrackedRequestByIdForRequesterAsync(
        int requestId,
        int requesterUserId,
        CancellationToken cancellationToken = default);

    Task<LibraryRequest?> GetTrackedRequestByIdAsync(
        int requestId,
        CancellationToken cancellationToken = default);

    Task<LibraryRequest?> GetTrackedRequestByIdForPossessorAsync(
        int requestId,
        int possessorUserId,
        CancellationToken cancellationToken = default);

    Task<LibraryRequest?> GetTrackedRequestWithUnitForCompleteAsync(
        int requestId,
        int possessorUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequestMessage>> GetLatestRequestMessagesAsync(
        int requestId,
        int limit,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequestMessage>> GetRequestMessagesBeforeIdAsync(
        int requestId,
        int beforeMessageId,
        int limit,
        CancellationToken cancellationToken = default);

    Task AddRequestMessageAsync(LibraryRequestMessage message, CancellationToken cancellationToken = default);

    Task<LibraryRequestMessage?> GetRequestMessageByIdWithAuthorAsync(
        int messageId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<int>> DeleteMessagesForRequestAsync(
        int requestId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequest>> GetTrackedCancellableRequestsForUnitAsync(
        int unitId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryRequest>> GetTrackedRequestsByRequesterAsync(
        int crewId,
        int requesterUserId,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<LibraryUnit>> GetTrackedUnitsPossessedByUserAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default);

    Task AddMaintenanceRecordAsync(
        LibraryMaintenanceRecord record,
        CancellationToken cancellationToken = default);

    Task CleanupMemberLibraryDataAsync(
        int crewId,
        int userId,
        CancellationToken cancellationToken = default);
}
