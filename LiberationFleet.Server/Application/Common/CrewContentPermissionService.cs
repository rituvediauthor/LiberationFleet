using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class CrewContentPermissionService
{
    public static bool CanAttachFilesToCrewContent(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        DateTime utcNow)
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

        if (GetTenureDays(membership, utcNow) < crew.MinimumCrewmateTenureDaysForAttachments)
        {
            return false;
        }

        return lifetimeContributions >= crew.MinimumContributionForAttachments;
    }

    public static bool CanCreateProposals(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        DateTime utcNow)
    {
        if (membership.IsOrganizer)
        {
            return true;
        }

        if (membership.CanCreateProposals)
        {
            return true;
        }

        if (GetTenureDays(membership, utcNow) < crew.MinimumCrewmateTenureDaysForProposals)
        {
            return false;
        }

        return lifetimeContributions >= crew.MinimumContributionForProposals;
    }

    public static bool NeedsAttachFilesPermissionGrant(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        DateTime utcNow) =>
        !membership.IsOrganizer
        && !membership.CanAttachFiles
        && !CanAttachFilesToCrewContent(crew, membership, lifetimeContributions, utcNow);

    public static bool NeedsCreateProposalsPermissionGrant(
        Crew crew,
        CrewMembership membership,
        decimal lifetimeContributions,
        DateTime utcNow) =>
        !membership.IsOrganizer
        && !membership.CanCreateProposals
        && !CanCreateProposals(crew, membership, lifetimeContributions, utcNow);

    public static int GetTenureDays(CrewMembership membership, DateTime utcNow) =>
        Math.Max(0, (int)Math.Floor((utcNow - membership.JoinedAt).TotalDays));
}
