using LiberationFleet.Server.Application.Features.Crypto.Contracts;

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
    public IReadOnlyList<string> AvailableActions { get; set; } = Array.Empty<string>();
    public IReadOnlyList<GiftPlatformOptionDto> CompletionPlatformOptions { get; set; } = Array.Empty<GiftPlatformOptionDto>();
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
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
