namespace LiberationFleet.Server.Application.Features.Recipients.Contracts;

public class ReceptionOrderResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public List<RecipientNeedDto> Recipients { get; set; } = new();
}

public class RecipientNeedDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    public bool IsSurvivalThreshold { get; set; }
    public int ReceptionOrderPosition { get; set; }
    public List<int> CommonPaymentPlatforms { get; set; } = new();
    public int? SuggestedMiddlemanId { get; set; }
    public string? SuggestedMiddlemanName { get; set; }
    public string PaymentNote { get; set; } = string.Empty;
}
