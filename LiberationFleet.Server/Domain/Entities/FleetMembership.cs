namespace LiberationFleet.Server.Domain.Entities;

/// <summary>
/// Direct fleet membership for users who are in a fleet without a crew (No-Crew).
/// Users with an active crew in the fleet are members via <see cref="FleetCrew"/> + <see cref="CrewMembership"/>.
/// </summary>
public class FleetMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FleetId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Fleet Fleet { get; set; } = null!;
}
