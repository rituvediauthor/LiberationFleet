using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class CrewRoleAuthorizationService
{
    public static bool CanModerateAttachments(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsModerator;

    public static bool CanToggleCanAttachFiles(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsModerator;

    public static bool CanExportCrewData(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsDecentralizer;

    public static bool CanProposeCrewmateAidStatEdits(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsAccountant;

    public static bool CanManagePlaceholders(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsAccountant;

    public static bool CanImpersonateGiftGiver(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsAccountant;

    public static bool CanBypassSeasonGiftLock(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsAccountant;

    /// <summary>Immediate reception-order editor (thresholds + cycles) — no proposal required.</summary>
    public static bool CanEditReceptionOrder(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsAccountant;
}
