namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Platform donation (Liberation Fleet app funding). Card data never touches our servers — Stripe Checkout only.
/// </summary>
public class AppDonation
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;

    /// <summary>Amount in USD cents.</summary>
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public string Status { get; set; } = "pending";
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }
}
