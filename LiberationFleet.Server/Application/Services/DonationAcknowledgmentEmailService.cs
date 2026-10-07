using System.Globalization;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Domain.Entities;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Services;

public class DonationAcknowledgmentEmailService(
    IEmailSender emailSender,
    IOptions<OrganizationOptions> organizationOptions,
    ILogger<DonationAcknowledgmentEmailService> logger) : IDonationAcknowledgmentEmailService
{
    public Task<bool> TrySendAsync(
        AppDonation donation,
        User user,
        CancellationToken cancellationToken = default)
    {
        var name = string.IsNullOrWhiteSpace(user.Username) ? "Friend" : user.Username.Trim();
        var email = !string.IsNullOrWhiteSpace(donation.ReceiptEmail)
            ? donation.ReceiptEmail
            : user.Email;
        return TrySendAsync(donation, email, name, cancellationToken);
    }

    public async Task<bool> TrySendAsync(
        AppDonation donation,
        string receiptEmail,
        string donorDisplayName,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(receiptEmail))
        {
            logger.LogWarning(
                "Skipping donation acknowledgment for donation {DonationId}: no receipt email.",
                donation.Id);
            return false;
        }

        var org = organizationOptions.Value;
        var legalName = string.IsNullOrWhiteSpace(org.LegalName) ? "Liberation Fleet Co." : org.LegalName.Trim();
        var completedAt = donation.CompletedAt ?? DateTime.UtcNow;
        var amount = donation.AmountCents / 100m;
        var currency = string.IsNullOrWhiteSpace(donation.Currency)
            ? "USD"
            : donation.Currency.Trim().ToUpperInvariant();
        var amountLine = currency == "USD"
            ? amount.ToString("C", CultureInfo.GetCultureInfo("en-US"))
            : $"{amount.ToString("0.00", CultureInfo.InvariantCulture)} {currency}";

        var displayName = string.IsNullOrWhiteSpace(donorDisplayName) ? "Friend" : donorDisplayName.Trim();
        var body = BuildBody(
            legalName,
            org,
            displayName,
            amountLine,
            completedAt,
            donation.Id,
            donation.StripePaymentIntentId);

        try
        {
            await emailSender.SendAsync(
                receiptEmail.Trim(),
                $"Donation acknowledgment from {legalName}",
                body,
                cancellationToken);
            return true;
        }
        catch (Exception ex)
        {
            logger.LogError(
                ex,
                "Failed to send donation acknowledgment for donation {DonationId} to {Email}",
                donation.Id,
                receiptEmail);
            return false;
        }
    }

    public static string BuildBody(
        string legalName,
        OrganizationOptions org,
        string username,
        string amountLine,
        DateTime completedAtUtc,
        int donationId,
        string? stripePaymentIntentId)
    {
        var dateLocal = completedAtUtc.ToString("MMMM d, yyyy", CultureInfo.GetCultureInfo("en-US"))
            + " (UTC)";

        var lines = new List<string>
        {
            $"Dear {username},",
            "",
            $"Thank you for your donation to {legalName}.",
            "",
            "This email is your written acknowledgment of your contribution for your tax records.",
            "",
            $"Organization: {legalName}"
        };

        if (!string.IsNullOrWhiteSpace(org.Ein))
        {
            lines.Add($"EIN: {org.Ein.Trim()}");
        }

        if (!string.IsNullOrWhiteSpace(org.MailingAddress))
        {
            lines.Add($"Address: {org.MailingAddress.Trim()}");
        }

        lines.Add($"Contribution date: {dateLocal}");
        lines.Add($"Contribution amount: {amountLine}");
        lines.Add($"Donation reference: #{donationId}");
        if (!string.IsNullOrWhiteSpace(stripePaymentIntentId))
        {
            lines.Add($"Payment reference: {stripePaymentIntentId.Trim()}");
        }

        lines.Add("");
        if (!string.IsNullOrWhiteSpace(org.TaxExemptStatement))
        {
            lines.Add(org.TaxExemptStatement.Trim());
            lines.Add("");
        }

        lines.Add(
            $"No goods or services were provided by {legalName} in exchange for this contribution.");
        lines.Add("");
        lines.Add(
            "Please retain this email for your records. Consult your tax advisor regarding deductibility.");
        lines.Add("");
        lines.Add($"With gratitude,");
        lines.Add(legalName);

        return string.Join('\n', lines);
    }
}
