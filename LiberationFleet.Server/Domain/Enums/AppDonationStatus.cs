namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// Lifecycle status for platform (Stripe Checkout) donations.
/// Stored as int in AppDonations.Status.
/// </summary>
public enum AppDonationStatus
{
    Pending = 0,
    Completed = 1,
    Failed = 2
}
