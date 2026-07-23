namespace LiberationFleet.Server.Domain.Enums;

public enum CrewPrivacy
{
    Public = 0,
    Private = 1,
    InviteOnly = 2,
    /// <summary>Only people already in the same fleet as this crew may join.</summary>
    FleetMembersOnly = 3
}
