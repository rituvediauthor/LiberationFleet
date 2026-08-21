namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// Kind of need shown in the mutual-aid reception / giving order.
/// Serialized to clients as camelCase strings (see <see cref="ToApiValue"/>).
/// </summary>
public enum ReceptionEntryType
{
    /// <summary>Monthly survival-threshold gift for crewmates with NeedsSurvivalAid.</summary>
    SurvivalThreshold = 0,

    /// <summary>Primary (or remaining) season cycle reception toward the member/non-member cap.</summary>
    Cycle = 1,

    /// <summary>
    /// Catch-up after a completed cycle when the effective cap has grown since completion
    /// (visible only after the monthly catch-up snapshot).
    /// </summary>
    CatchUp = 2,

    /// <summary>
    /// Active elected Representative term: unlimited need so the representative can receive
    /// gifts outside normal cycle caps for the duration of their term.
    /// </summary>
    Representative = 3
}

public static class ReceptionEntryTypeExtensions
{
    public static string ToApiValue(this ReceptionEntryType entryType) => entryType switch
    {
        ReceptionEntryType.SurvivalThreshold => "survivalThreshold",
        ReceptionEntryType.Cycle => "cycle",
        ReceptionEntryType.CatchUp => "catchUp",
        ReceptionEntryType.Representative => "representative",
        _ => entryType.ToString()
    };

    public static bool TryParseApiValue(string? value, out ReceptionEntryType entryType)
    {
        entryType = default;
        if (string.IsNullOrWhiteSpace(value))
        {
            return false;
        }

        switch (value.Trim().ToLowerInvariant())
        {
            case "survivalthreshold":
                entryType = ReceptionEntryType.SurvivalThreshold;
                return true;
            case "cycle":
                entryType = ReceptionEntryType.Cycle;
                return true;
            case "catchup":
                entryType = ReceptionEntryType.CatchUp;
                return true;
            case "representative":
                entryType = ReceptionEntryType.Representative;
                return true;
            default:
                return false;
        }
    }
}
