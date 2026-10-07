using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public interface IDonationAcknowledgmentEmailService
{
    /// <summary>
    /// Sends a contemporaneous written acknowledgment to the account holder. Does not throw on send failure.
    /// </summary>
    Task<bool> TrySendAsync(AppDonation donation, User user, CancellationToken cancellationToken = default);

    /// <summary>
    /// Sends acknowledgment to an explicit receipt email (guest or account). Does not throw on send failure.
    /// </summary>
    Task<bool> TrySendAsync(
        AppDonation donation,
        string receiptEmail,
        string donorDisplayName,
        CancellationToken cancellationToken = default);
}
