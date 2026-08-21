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
    Task<GiftLogPage> GetLogPageByCrewIdsAsync(
        IReadOnlyList<int> crewIds,
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
    Task<IReadOnlyList<PendingReceptionCredit>> GetPendingReceptionCreditsAsync(
        int crewId,
        DateTime? currentSeasonStartDate,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<Gift>> GetGiftsDueForAutoVerificationAsync(
        DateTime createdBeforeUtc,
        int limit = 100,
        CancellationToken cancellationToken = default);

    Task<IReadOnlyList<GiftComment>> GetCommentsByGiftIdAsync(int giftId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<GiftComment>> GetRepliesByParentCommentIdAsync(
        int giftId,
        int parentCommentId,
        CancellationToken cancellationToken = default);
    Task<GiftComment?> GetCommentByIdAsync(int commentId, CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetActiveLikeCountsForGiftsAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetActiveLikedGiftIdsByUserAsync(
        int userId,
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetActiveLikeCountsForGiftCommentsAsync(
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default);
    Task<HashSet<int>> GetActiveLikedGiftCommentIdsByUserAsync(
        int userId,
        IEnumerable<int> commentIds,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, int>> GetCommentCountsForGiftsAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default);
    Task<GiftLike?> GetGiftLikeAsync(int userId, int giftId, CancellationToken cancellationToken = default);
    Task<GiftLike?> GetGiftCommentLikeAsync(int userId, int commentId, CancellationToken cancellationToken = default);
    Task AddLikeAsync(GiftLike like, CancellationToken cancellationToken = default);
    Task AddCommentAsync(GiftComment comment, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(int UserId, string Username, string? AvatarResourceId)>> GetActiveGiftLikersAsync(
        int giftId,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyList<(int UserId, string Username, string? AvatarResourceId)>> GetActiveGiftCommentLikersAsync(
        int commentId,
        CancellationToken cancellationToken = default);
    Task<Dictionary<int, DateTime?>> GetSeasonStartDatesForGiftsAsync(
        IEnumerable<int> giftIds,
        CancellationToken cancellationToken = default);
}

public sealed class GiftRecipientSummary
{
    public int RecipientUserId { get; init; }
    public string RecipientUsername { get; init; } = string.Empty;
    public decimal TotalAmount { get; init; }
    public int GiftCount { get; init; }
    public DateTime LastGiftAt { get; init; }
}

public sealed class PendingReceptionCredit
{
    public int RecipientUserId { get; init; }
    public int? SeasonCycleId { get; init; }
    public bool IsSurvivalThreshold { get; init; }
    public bool IsRepresentativeGift { get; init; }
    public decimal Amount { get; init; }
}
