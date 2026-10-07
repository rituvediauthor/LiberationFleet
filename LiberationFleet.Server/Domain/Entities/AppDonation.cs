using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Platform donation (Liberation Fleet app funding). Card data never touches our servers — Stripe Checkout only.
/// Guests may donate with <see cref="UserId"/> null and a <see cref="ReceiptEmail"/>.
/// </summary>
public class AppDonation
{
    public int Id { get; set; }
    public int? UserId { get; set; }
    public User? User { get; set; }

    /// <summary>Email for the written donation acknowledgment (account or guest-provided).</summary>
    public string ReceiptEmail { get; set; } = string.Empty;

    /// <summary>Amount in USD cents.</summary>
    public long AmountCents { get; set; }
    public string Currency { get; set; } = "usd";
    public AppDonationStatus Status { get; set; } = AppDonationStatus.Pending;
    public string? StripeCheckoutSessionId { get; set; }
    public string? StripePaymentIntentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? CompletedAt { get; set; }

    /// <summary>When the donor tax acknowledgment email was successfully accepted by the mail sender.</summary>
    public DateTime? AcknowledgmentEmailSentAt { get; set; }
}
