using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Application.Features.Engagement.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Contracts;

namespace LiberationFleet.Server.Application.Features.Gifts.Contracts;

public class CrewMemberDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public IReadOnlyList<int> PlatformIds { get; set; } = Array.Empty<int>();
}

public class GiftLogEntryDto
{
    public int Id { get; set; }
    public string Type { get; set; } = string.Empty;
    public int GiverId { get; set; }
    public string GiverName { get; set; } = string.Empty;
    public int RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public int? MiddlemanId { get; set; }
    public string? MiddlemanName { get; set; }
    public decimal Amount { get; set; }
    public string Platform { get; set; } = string.Empty;
    public DateTime Timestamp { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<int> RelatedUserIds { get; set; } = Array.Empty<int>();
    public string? Status { get; set; }
    public string VerificationStatus { get; set; } = string.Empty;
    public string? DisplayFlag { get; set; }
    public string? CustomGiftCategory { get; set; }
    public IReadOnlyList<string> AvailableActions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<GiftPlatformOptionDto> CompletionPlatformOptions { get; set; } = Array.Empty<GiftPlatformOptionDto>();
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
    public int LikeCount { get; set; }
    public bool LikedByCurrentUser { get; set; }
    public int CommentCount { get; set; }
    public bool IsSeasonLocked { get; set; }
}

public class GiftCommentDto
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
    public int LikeCount { get; set; }
    public bool LikedByCurrentUser { get; set; }
}

public class GiftDetailDto : GiftLogEntryDto
{
    public IReadOnlyList<GiftCommentDto> Comments { get; set; } = Array.Empty<GiftCommentDto>();
}

public class GiftDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GiftDetailDto? Entry { get; set; }
}

public class GiftCommentRepliesResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<GiftCommentDto> Items { get; set; } = Array.Empty<GiftCommentDto>();
}

public class GiftEngagementOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? CommentId { get; set; }
}

public class GiftLikeToggleResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool Liked { get; set; }
    public int LikeCount { get; set; }
}

public class CreateGiftCommentRequest
{
    public int? ParentCommentId { get; set; }
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public List<int> MentionedUserIds { get; set; } = [];
    public string? NotificationPreview { get; set; }
}

public class SeasonProfileDto
{
    public IReadOnlyList<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = Array.Empty<PaymentPlatformAccountDto>();
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public IReadOnlyList<string> IdentityGroups { get; set; } = Array.Empty<string>();
    public bool NeedsSurvivalAid { get; set; }
    public bool CanToggleInNeedOff { get; set; }
    public decimal InNeedToggleThreshold { get; set; }
    public decimal EstimatedMonthlyContribution { get; set; }
    public bool CanEditEstimatedContribution { get; set; }
    public DateTime? GivingSeasonJoinedAt { get; set; }
    public int PriorityScore { get; set; }
}

public class SeasonProfileResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SeasonProfileDto? Profile { get; set; }
}

public class UpdateSeasonProfileRequest
{
    public List<PaymentPlatformAccountDto> PaymentPlatforms { get; set; } = [];
    public bool InNeedOfAid { get; set; }
    public int EmergencyLevel { get; set; }
    public int PeopleRepresentedCount { get; set; } = 1;
    public int DisabilityLevel { get; set; }
    public List<string> IdentityGroups { get; set; } = [];
    public bool NeedsSurvivalAid { get; set; }
    public decimal EstimatedMonthlyContribution { get; set; }
}

public class GiftPlatformOptionDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
}

public class PendingMiddlemanGiftDto
{
    public int Id { get; set; }
    public int InitiatorId { get; set; }
    public string InitiatorName { get; set; } = string.Empty;
    public int RecipientId { get; set; }
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public string? Platform { get; set; }
}

public class GiftLogResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<GiftLogEntryDto> Items { get; set; } = Array.Empty<GiftLogEntryDto>();
    public bool HasMore { get; set; }
}

public class GiftHistoryRecipientSummaryDto
{
    public int RecipientUserId { get; set; }
    public string RecipientUsername { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public int GiftCount { get; set; }
    public DateTime LastGiftAt { get; set; }
}

public class GiftHistoryRecipientListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<GiftHistoryRecipientSummaryDto> Items { get; set; } = Array.Empty<GiftHistoryRecipientSummaryDto>();
}

public class GiftHistoryEntryDto
{
    public int Id { get; set; }
    public decimal Amount { get; set; }
    public DateTime Timestamp { get; set; }
    public string GiftType { get; set; } = string.Empty;
    public string Platform { get; set; } = string.Empty;
    public string? MiddlemanUsername { get; set; }
    public string StatusLabel { get; set; } = string.Empty;
}

public class GiftHistoryDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RecipientUserId { get; set; }
    public string RecipientUsername { get; set; } = string.Empty;
    public decimal TotalAmount { get; set; }
    public IReadOnlyList<GiftHistoryEntryDto> Items { get; set; } = Array.Empty<GiftHistoryEntryDto>();
}

public class GiftOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GiftLogEntryDto? Entry { get; set; }
}
