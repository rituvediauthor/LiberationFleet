using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public interface IDonationAcknowledgmentEmailService
{
    /// <summary>
    /// Sends a contemporaneous written acknowledgment to the donor. Does not throw on send failure.
    /// Returns true when an email was accepted by the sender.
    /// </summary>
    Task<bool> TrySendAsync(AppDonation donation, User user, CancellationToken cancellationToken = default);
}
