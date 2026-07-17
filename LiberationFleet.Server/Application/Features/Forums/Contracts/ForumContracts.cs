using LiberationFleet.Server.Application.Features.Crypto.Contracts;

namespace LiberationFleet.Server.Application.Features.Forums.Contracts;

public class ForumListItemDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public string? AuthorAvatarResourceId { get; set; }
    public DateTime LastActivityAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
    public string? Title { get; set; }
    public string? Body { get; set; }
    public bool IsAdultContent { get; set; }
}

public class ForumDetailDto : ForumListItemDto
{
    public DateTime CreatedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public IReadOnlyList<ForumCommentDto> Comments { get; set; } = Array.Empty<ForumCommentDto>();
}

public class ForumCommentDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public string? AuthorAvatarResourceId { get; set; }
    public int? ParentCommentId { get; set; }
    public int? ReplyToCommentId { get; set; }
    public string? ReplyToUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ReplyCount { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
    public string? Body { get; set; }
}

public class ForumListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ForumListItemDto> Items { get; set; } = Array.Empty<ForumListItemDto>();
}

public class ForumDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ForumDetailDto? Post { get; set; }
}

public class ForumOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? PostId { get; set; }
    public int? CommentId { get; set; }
}

public class CreateForumPostRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public bool IsAdultContent { get; set; }
    public List<int> MentionedUserIds { get; set; } = [];
    public string? NotificationPreview { get; set; }
}

public class CreateFleetForumPostRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public bool IsAdultContent { get; set; }
    public List<int> MentionedUserIds { get; set; } = [];
    public string? NotificationPreview { get; set; }
}

public class UpdateForumPostRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class UpdateFleetForumPostRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class CreateForumCommentRequest
{
    public int? ParentCommentId { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
    public string? NotificationPreview { get; set; }
}

public class CreateFleetForumCommentRequest
{
    public int? ParentCommentId { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
    public string? NotificationPreview { get; set; }
}

public class UpdateForumCommentRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class UpdateFleetForumCommentRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class ForumCommentRepliesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ForumCommentDto> Items { get; set; } = Array.Empty<ForumCommentDto>();
}
