using LiberationFleet.Server.Application.Features.Crypto.Contracts;

namespace LiberationFleet.Server.Application.Features.Projects.Contracts;

public class ProjectListItemDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime LastActivityAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
}

public class ProjectDetailDto : ProjectListItemDto
{
    public DateTime CreatedAt { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public IReadOnlyList<ProjectCommentDto> Comments { get; set; } = Array.Empty<ProjectCommentDto>();
}

public class ProjectCommentDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ReplyCount { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
}

public class ProjectListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ProjectListItemDto> Items { get; set; } = Array.Empty<ProjectListItemDto>();
}

public class ProjectDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ProjectDetailDto? Post { get; set; }
}

public class ProjectOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? PostId { get; set; }
    public int? CommentId { get; set; }
}

public class CreateProjectPostRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class UpdateProjectPostRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class CreateProjectCommentRequest
{
    public int? ParentCommentId { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class UpdateProjectCommentRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class ProjectCommentRepliesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ProjectCommentDto> Items { get; set; } = Array.Empty<ProjectCommentDto>();
}
