using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

public static class CrewRoleMapper
{
    public static IReadOnlyList<string> MapRoles(CrewMembership membership)
    {
        var roles = new List<string>();
        if (membership.IsOrganizer)
        {
            roles.Add("Organizer");
        }

        if (membership.IsHonoraryMember)
        {
            roles.Add("Honorary member");
        }

        return roles;
    }
}
