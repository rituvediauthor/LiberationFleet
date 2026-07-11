using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Activity.Contracts;

public class UserActivityItemDto
{
    public string Key { get; set; } = string.Empty;
    public UserActivityKind Kind { get; set; }
    public UserActivityFilterCategory Category { get; set; }
    public string Label { get; set; } = string.Empty;
    public string? Detail { get; set; }
    public DateTime CreatedAt { get; set; }
    public int CrewId { get; set; }
    public int ResourceId { get; set; }
    public int? ParentResourceId { get; set; }
    public int? RelatedUserId { get; set; }
    public ChatRoomType? ChatRoomType { get; set; }
    public int? LibraryUnitId { get; set; }
    public bool IsAccessible { get; set; }
    public string? PreviewContentType { get; set; }
    public string? ThumbnailResourceId { get; set; }
    public string? PlaintextPreview { get; set; }
}

public class UserActivityListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<UserActivityItemDto> Items { get; set; } = Array.Empty<UserActivityItemDto>();
    public bool HasMore { get; set; }
}
