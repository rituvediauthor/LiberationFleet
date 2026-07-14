using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class CrewContentPermissionService
{
    public static bool CanAttachFilesToCrewContent(
        Crew crew,
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

        if (!crew.AllowCrewmateFileAttachments)
        {
            return false;
        }

        if (tenureDays < crew.MinimumCrewmateTenureDaysForAttachments)
        {
            return false;
        }

        return lifetimeContributions >= crew.MinimumContributionForAttachments;
    }

    public static bool CanCreateProposals(
        Crew crew,
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

        if (tenureDays < crew.MinimumCrewmateTenureDaysForProposals)
        {
            return false;
        }

        return lifetimeContributions >= crew.MinimumContributionForProposals;
    }

    public static bool NeedsAttachFilesPermissionGrant(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        int tenureDays) =>
        !membership.IsOrganizer
        && !membership.CanAttachFiles
        && !CanAttachFilesToCrewContent(crew, membership, lifetimeContributions, tenureDays);

    public static bool NeedsCreateProposalsPermissionGrant(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        int tenureDays) =>
        !membership.IsOrganizer
        && !membership.CanCreateProposals
        && !CanCreateProposals(crew, membership, lifetimeContributions, tenureDays);
}
