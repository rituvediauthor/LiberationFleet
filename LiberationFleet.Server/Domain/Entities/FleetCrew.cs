namespace LiberationFleet.Server.Domain.Entities;

public class FleetCrew
{
    public int Id { get; set; }
    public int FleetId { get; set; }
    public int CrewId { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public Fleet Fleet { get; set; } = null!;
    public Crew Crew { get; set; } = null!;
}
