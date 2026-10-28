using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Notifications.Contracts;

public class NotificationDto
{
    public int Id { get; set; }
    public int? CrewId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
    public int? SecondaryEntityId { get; set; }
    public int? ActorUserId { get; set; }
    public string? ActorAvatarResourceId { get; set; }
    public bool IsRead { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class NotificationListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<NotificationDto> Items { get; set; } = Array.Empty<NotificationDto>();
    public int UnreadCount { get; set; }
}

public class NotificationPreferenceDto
{
    public NotificationKind Kind { get; set; }
    public string Label { get; set; } = string.Empty;
    public string Category { get; set; } = "Crew";
    public bool IsEnabled { get; set; }
}

public class NotificationPreferencesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<NotificationPreferenceDto> Preferences { get; set; } = Array.Empty<NotificationPreferenceDto>();
}

public class UpdateNotificationPreferencesRequest
{
    public IReadOnlyList<NotificationPreferenceDto> Preferences { get; set; } = Array.Empty<NotificationPreferenceDto>();
    public string? SettingsPassword { get; set; }
}

public class NotificationOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
}

public class CreateNotificationRequest
{
    public int UserId { get; set; }
    public int? CrewId { get; set; }
    public NotificationKind Kind { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Body { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
    public int? RelatedEntityId { get; set; }
    public int? SecondaryEntityId { get; set; }
    public int? ActorUserId { get; set; }

    /// <summary>
    /// When true, skip this standard notification if the recipient has mention
    /// (or fleet mention) notifications enabled. They get the mention instead.
    /// </summary>
    public bool RecipientWasMentioned { get; set; }

    /// <summary>When set, skip this notification if the recipient muted that post.</summary>
    public int? MuteForumPostId { get; set; }

    /// <summary>When set, skip this notification if the recipient muted that crew's posts.</summary>
    public int? MuteCrewSpaceId { get; set; }

    /// <summary>When set, skip this notification if the recipient muted that fleet's posts.</summary>
    public int? MuteFleetSpaceId { get; set; }
}

public class NotificationBadgeSummaryResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int UnreadCount { get; set; }
    public Dictionary<string, int> AreaCounts { get; set; } = new();
    public Dictionary<string, int> ResourceCounts { get; set; } = new();
}

public class MarkNotificationsReadByContentRequest
{
    public string? ActionUrlPrefix { get; set; }
    public int? RelatedEntityId { get; set; }
}

public class MutedContentDto
{
    public MutedContentType ContentType { get; set; }
    public int ResourceId { get; set; }
    public DateTime? MutedUntil { get; set; }
}

public class MutedContentListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<MutedContentDto> Items { get; set; } = Array.Empty<MutedContentDto>();
}

public class SetMutedContentRequest
{
    public MutedContentType ContentType { get; set; }
    public int ResourceId { get; set; }
    public bool Muted { get; set; }
    /// <summary>Ignored when <see cref="Muted"/> is false. Null means the mute does not expire.</summary>
    public MuteDuration? Duration { get; set; }
}

public class HiddenContentDto
{
    public MutedContentType ContentType { get; set; }
    public int ResourceId { get; set; }
}

public class HiddenContentListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<HiddenContentDto> Items { get; set; } = Array.Empty<HiddenContentDto>();
}

public class SetHiddenContentRequest
{
    public MutedContentType ContentType { get; set; }
    public int ResourceId { get; set; }
    public bool Hidden { get; set; }
}

/// <summary>
/// Optional body for like toggles. Client should send a truncated plaintext sample of the
/// liked content so encrypted posts/messages can still show a preview in the notification.
/// </summary>
public class ContentLikeRequest
{
    public string? NotificationPreview { get; set; }
}
