namespace LiberationFleet.Server.Domain;

/// <summary>
/// Built-in default so crewmates can receive aid without a third-party finance app.
/// </summary>
public static class PaymentPlatformDefaults
{
    public const string CashCheckName = "Cash/check";
    public const string InPersonHandle = "In Person";
}
