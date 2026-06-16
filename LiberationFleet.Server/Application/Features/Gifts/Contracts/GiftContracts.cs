namespace LiberationFleet.Server.Application.Features.Gifts.Contracts;

public class CrewMemberDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
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
}

public class GiftOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public GiftLogEntryDto? Entry { get; set; }
}
