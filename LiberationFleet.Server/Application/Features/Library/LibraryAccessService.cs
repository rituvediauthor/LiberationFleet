using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Library;

public static class LibraryAccessService
{
    public const string DisabledMessage = "Library of Things is disabled for this crew.";
    public const string SeasonNotStartedMessage = "Start a season of giving to unlock Library of Things.";
    public const string NotInSeasonMessage = "Join the current season of giving to unlock Library of Things.";

    public static string? GetAccessDeniedMessage(Crew crew, CrewMembership membership)
    {
        if (!crew.LibraryOfThingsEnabled)
        {
            return DisabledMessage;
        }

        if (!crew.SeasonStarted)
        {
            return SeasonNotStartedMessage;
        }

        if (!membership.IsInSeason)
        {
            return NotInSeasonMessage;
        }

        return null;
    }
}
