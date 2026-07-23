using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common;

public static class PrivacyAccess
{
    public static bool CanDiscoverByBrowse(CrewPrivacy privacy) =>
        privacy == CrewPrivacy.Public;

    /// <summary>
    /// Private crews/fleets can be reached with an exact join code.
    /// Invite-only and fleet-members-only cannot be discovered this way.
    /// </summary>
    public static bool CanDiscoverByJoinCode(CrewPrivacy privacy) =>
        privacy is CrewPrivacy.Public or CrewPrivacy.Private;

    public static bool IsFleetScopedCrewPrivacy(CrewPrivacy privacy) =>
        privacy == CrewPrivacy.FleetMembersOnly;

    public static string InviteOnlyJoinMessage(string entityName) =>
        $"This {entityName} is invite-only. You must receive an invitation to join.";

    public static string FleetMembersOnlyJoinMessage() =>
        "This crew is for fleet members only. You must already be in the same fleet to join.";
}
