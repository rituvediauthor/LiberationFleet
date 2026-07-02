using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryEnumParser
{
    public static bool TryParseOfferingKind(string? value, out LibraryOfferingKind kind)
    {
        kind = LibraryOfferingKind.Durable;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out kind);
    }

    public static bool TryParseFulfillmentMode(string? value, out LibraryFulfillmentMode mode)
    {
        mode = LibraryFulfillmentMode.OnRequest;
        if (string.IsNullOrWhiteSpace(value))
        {
            return true;
        }

        return Enum.TryParse(value.Trim(), ignoreCase: true, out mode);
    }
}
