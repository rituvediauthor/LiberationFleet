using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class FleetContentPermissionService
{
    public static bool CanAttachFilesToFleetContent(
        Fleet fleet,
        CrewMembership membership,
        decimal lifetimeContributions,
        int tenureDays)
    {
        if (membership.IsOrganizer)
        {
            return true;
        }

        if (membership.CanAttachFiles)
        {
            return true;
        }

        if (!fleet.AllowCrewmateFileAttachments)
        {
            return false;
        }

        if (tenureDays < fleet.MinimumCrewmateTenureDaysForAttachments)
        {
            return false;
        }

        return lifetimeContributions >= fleet.MinimumContributionForAttachments;
    }

    public static bool CanCreateProposals(
        Fleet fleet,
        CrewMembership membership,
        decimal lifetimeContributions,
        int tenureDays)
    {
        if (membership.IsOrganizer)
        {
            return true;
        }

        if (membership.CanCreateProposals)
        {
            return true;
        }

        if (tenureDays < fleet.MinimumCrewmateTenureDaysForProposals)
        {
            return false;
        }

        return lifetimeContributions >= fleet.MinimumContributionForProposals;
    }
}
