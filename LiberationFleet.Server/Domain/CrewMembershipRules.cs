using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Domain;

/// <summary>
/// Current-member checks for crew membership rows that may be soft-left or banned.
/// Prefer these helpers in comments; EF queries must inline the same predicates.
/// </summary>
public static class CrewMembershipRules
{
    /// <summary>Active in the crew right now (not banned, not soft-left).</summary>
    public static bool IsCurrentMember(CrewMembership membership) =>
        !membership.IsBanned && membership.LeftAt is null;
}
