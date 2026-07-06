using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common;

public static class AdultContentAccess
{
    public static bool IsBlocked(AdultContentPreference preference, bool isAdultContent) =>
        isAdultContent && preference == AdultContentPreference.Block;
}
