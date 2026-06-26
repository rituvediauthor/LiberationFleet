namespace LiberationFleet.Server.Domain.Entities;

public class CrewRule
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public Crew Crew { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
