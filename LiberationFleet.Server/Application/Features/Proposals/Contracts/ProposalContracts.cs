using LiberationFleet.Server.Application.Features.Crypto.Contracts;

namespace LiberationFleet.Server.Application.Features.Proposals.Contracts;

public class ProposalListItemDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime LastActivityAt { get; set; }
    public string Status { get; set; } = string.Empty;
    public int ApproveCount { get; set; }
    public int DisapproveCount { get; set; }
    public DateTime? ApprovalTimerEndsAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public bool HasPlaintextContent { get; set; }
    public string? Title { get; set; }
    public string? DescriptionPreview { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
    public string? CurrentUserVote { get; set; }
}

public class ProposalDetailDto : ProposalListItemDto
{
    public DateTime CreatedAt { get; set; }
    public string? Description { get; set; }
    public bool CanEdit { get; set; }
    public bool CanDelete { get; set; }
    public bool UsesAnonymousComments { get; set; }
    public string? ViewerAlias { get; set; }
    public bool CanKickAuthor { get; set; }
    public bool CanVote { get; set; } = true;
    public bool IsKickVoteTarget { get; set; }
    public IReadOnlyList<ProposalCommentDto> Comments { get; set; } = Array.Empty<ProposalCommentDto>();
}

public class ProposalCommentDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public int? ParentCommentId { get; set; }
    public int? ReplyToCommentId { get; set; }
    public string? ReplyToUsername { get; set; }
    public DateTime CreatedAt { get; set; }
    public int ReplyCount { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
    public bool IsOwnComment { get; set; }
    public bool CanKick { get; set; }
}

public class ProposalListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ProposalListItemDto> Items { get; set; } = Array.Empty<ProposalListItemDto>();
}

public class ProposalDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public ProposalDetailDto? Proposal { get; set; }
}

public class ProposalOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ProposalId { get; set; }
    public int? CommentId { get; set; }
    public string? Alias { get; set; }
    public ProposalDetailDto? Proposal { get; set; }
}

public class CreateProposalRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class UpdateProposalRequest
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class VoteProposalRequest
{
    public string Vote { get; set; } = string.Empty;
}

public class KickProposalRequest
{
    public string Reason { get; set; } = string.Empty;
}

public class CreateProposalCommentRequest
{
    public int? ParentCommentId { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}

public class UpdateProposalCommentRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
}
