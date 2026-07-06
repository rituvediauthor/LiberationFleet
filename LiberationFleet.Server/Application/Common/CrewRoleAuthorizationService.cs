using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common;

public static class CrewRoleAuthorizationService
{
    public static bool CanToggleAnonymousChat(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsAdvocate;

    public static bool CanModerateAttachments(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsModerator;

    public static bool CanToggleCanAttachFiles(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsModerator;

    public static bool CanExportCrewData(CrewMembership membership) =>
        membership.IsOrganizer || membership.IsDecentralizer;
}
