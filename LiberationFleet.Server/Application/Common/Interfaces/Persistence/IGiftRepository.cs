using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IGiftRepository
{
    Task<GiftLogPage> GetLogPageByCrewIdAsync(
        int crewId,
        int limit,
        DateTime? beforeCreatedAt = null,
        int? beforeId = null,
        CancellationToken cancellationToken = default);
    Task<IReadOnlySet<int>> GetCompletedInitiatedGiftIdsAsync(int crewId, CancellationToken cancellationToken = default);
    Task<Gift?> GetByIdWithUsersAsync(int id, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Gift>> GetPendingMiddlemanGiftsAsync(int middlemanUserId, int crewId, CancellationToken cancellationToken = default);
    Task<bool> HasCompletedInitiatedGiftAsync(int initiatedGiftId, CancellationToken cancellationToken = default);
    Task<Gift?> GetCompletedGiftForInitiatedAsync(int initiatedGiftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<int, Gift>> GetCompletedGiftsByInitiatedIdsAsync(int crewId, CancellationToken cancellationToken = default);
    Task AddAsync(Gift gift, CancellationToken cancellationToken = default);
    Task<UserGiftStats> GetUserGiftStatsAsync(int userId, CancellationToken cancellationToken = default);
    Task<CrewmateGiftStatsDto> GetCrewmateGiftStatsAsync(
        int userId,
        int crewId,
        DateTime? seasonStartDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GiftRecipientSummary>> GetGiverRecipientSummariesAsync(
        int giverUserId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Gift>> GetGiftsByGiverAndRecipientAsync(
        int giverUserId,
        int recipientUserId,
        CancellationToken cancellationToken = default);
    Task ReassignPlaceholderGiftRecipientsAsync(
        int crewId,
        int fromUserId,
        int toUserId,
        CancellationToken cancellationToken = default);
}

public sealed class GiftRecipientSummary
{
    public int RecipientUserId { get; init; }
    public string RecipientUsername { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
}
