namespace LiberationFleet.Server.Domain.Entities;

public class UserFleetContentTenure
{
    public int UserId { get; set; }
    public int FleetId { get; set; }
    public long AccruedTicks { get; set; }
    public DateTime? ClockStartedAtUtc { get; set; }

    public User User { get; set; } = null!;
    public Fleet Fleet { get; set; } = null!;
}
