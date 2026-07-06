namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;

public class EmergencyRequestListItemDto
{
    public int Id { get; set; }
    public int RequesterUserId { get; set; }
    public string RequesterUsername { get; set; } = string.Empty;
    public string PurposePreview { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    public decimal AmountFulfilled { get; set; }
    public decimal AmountRemaining { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class EmergencyRequestListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<EmergencyRequestListItemDto> Items { get; set; } = Array.Empty<EmergencyRequestListItemDto>();
}

public class EmergencyPlatformDto
{
    public int PlatformId { get; set; }
    public string PlatformName { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
    public bool IsSharedWithViewer { get; set; }
}

public class EmergencyMiddlemanOptionDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public IReadOnlyList<int> CommonPlatformIds { get; set; } = Array.Empty<int>();
}

public class EmergencyRequestDetailDto
{
    public int Id { get; set; }
    public int RequesterUserId { get; set; }
    public string RequesterUsername { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    public decimal AmountFulfilled { get; set; }
    public decimal AmountRemaining { get; set; }
    public string Status { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public IReadOnlyList<EmergencyPlatformDto> CommonPlatforms { get; set; } = Array.Empty<EmergencyPlatformDto>();
    public IReadOnlyList<EmergencyMiddlemanOptionDto> MiddlemanOptions { get; set; } = Array.Empty<EmergencyMiddlemanOptionDto>();
    public bool IsSelfRequest { get; set; }
}

public class EmergencyRequestDetailResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public EmergencyRequestDetailDto? Request { get; set; }
}

public class CreateEmergencyRequestRequest
{
    public string Purpose { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
}

public class EmergencyRequestOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int RequestId { get; set; }
}

public class RecordEmergencyGiftRequest
{
    public decimal Amount { get; set; }
    public int PaymentPlatformId { get; set; }
    public int? MiddlemanId { get; set; }
}

public class SubmitEmergencySplitRequest
{
    public decimal Amount { get; set; }
}
