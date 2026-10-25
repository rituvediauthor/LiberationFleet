using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class AttachmentPermissionReason
{
    public const string Disabled = "disabled";
    public const string Requirements = "requirements";
    public const string Blocked = "blocked";

    public static string? ForCrew(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        int tenureDays)
    {
        if (CrewContentPermissionService.CanAttachFilesToCrewContent(
                crew,
                membership,
                lifetimeContributions,
                tenureDays))
        {
            return null;
        }

        return Resolve(membership, crew.AllowCrewmateFileAttachments);
    }

    public static string? ForFleet(
        Fleet fleet,
        CrewMembership membership,
        decimal lifetimeContributions,
        int tenureDays)
    {
        if (FleetContentPermissionService.CanAttachFilesToFleetContent(
                fleet,
                membership,
                lifetimeContributions,
                tenureDays))
        {
            return null;
        }

        return Resolve(membership, fleet.AllowCrewmateFileAttachments);
    }

    private static string Resolve(CrewMembership membership, bool allowAttachments)
    {
        if (membership.AttachmentsDenied)
        {
            return Blocked;
        }

        return allowAttachments ? Requirements : Disabled;
    }
}
