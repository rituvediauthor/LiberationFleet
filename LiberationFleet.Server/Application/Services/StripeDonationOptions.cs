namespace LiberationFleet.Server.Application.Services;

public class StripeDonationOptions
{
    public const string SectionName = "Stripe";

    /// <summary>Secret key (sk_...). Never expose to clients.</summary>
    public string SecretKey { get; set; } = string.Empty;

    /// <summary>Webhook signing secret (whsec_...).</summary>
    public string WebhookSecret { get; set; } = string.Empty;

    /// <summary>Public site origin used to build success/cancel URLs, e.g. https://app.liberationfleet.com</summary>
    public string PublicAppBaseUrl { get; set; } = "https://localhost:49236";

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(SecretKey)
        && !SecretKey.Contains("change-me", StringComparison.OrdinalIgnoreCase);
}
