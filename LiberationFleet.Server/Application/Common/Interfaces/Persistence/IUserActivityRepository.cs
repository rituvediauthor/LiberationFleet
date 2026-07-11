using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IUserActivityRepository
{
    Task<IReadOnlyList<UserActivityRecord>> GetUserActivitiesAsync(
        int userId,
        UserActivityFilterCategory category,
        DateTime? beforeCreatedAt,
        string? beforeKey,
        int limit,
        CancellationToken cancellationToken = default);
}

public sealed class UserActivityRecord
{
    public string Key { get; init; } = string.Empty;
    public UserActivityKind Kind { get; init; }
    public UserActivityFilterCategory Category { get; init; }
    public string Label { get; init; } = string.Empty;
    public string? Detail { get; init; }
    public DateTime CreatedAt { get; init; }
    public int CrewId { get; init; }
    public int ResourceId { get; init; }
    public int? ParentResourceId { get; init; }
    public int? RelatedUserId { get; init; }
    public ChatRoomType? ChatRoomType { get; init; }
    public int? LibraryUnitId { get; init; }
    public bool ResourceExists { get; init; } = true;
    public EncryptedContentType? PreviewContentType { get; init; }
    public string? ThumbnailResourceId { get; init; }
    public string? PlaintextPreview { get; init; }
}
