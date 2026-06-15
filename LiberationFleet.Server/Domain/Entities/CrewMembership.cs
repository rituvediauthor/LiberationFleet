namespace LiberationFleet.Server.Domain.Entities;

public class CrewMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CrewId { get; set; }
    public bool IsBanned { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Crew Crew { get; set; } = null!;
}
