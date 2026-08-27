namespace LiberationFleet.Server.Application.Common;

/// <summary>
/// Canonical plaintext length limits for user-facing inputs.
/// Keep in sync with liberationfleet.client/src/app/utils/text-field-limits.ts.
/// </summary>
public static class TextFieldLimits
{
    public const int OrgName = 100;
    public const int ChatRoomName = 120;
    public const int ChatRoomPurpose = 2000;
    public const int Title = 200;
    public const int LongBody = 10000;
    public const int FleetRuleDescription = 4000;
    public const int Message = 2000;
    public const int ShortPurpose = 2000;
    public const int Note = 1000;
    public const int PaymentHandle = 128;
    public const int PaymentPlatformName = 64;
    public const int ShortLabel = 64;
    public const int PlaceholderDisplayName = 256;
}
