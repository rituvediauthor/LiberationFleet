namespace LiberationFleet.Server.Domain.Entities;

public class UserCrewContentTenure
{
    public int UserId { get; set; }
    public int CrewId { get; set; }
    public long AccruedTicks { get; set; }
    public DateTime? ClockStartedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Crew Crew { get; set; } = null!;
}
