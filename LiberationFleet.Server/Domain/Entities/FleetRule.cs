namespace LiberationFleet.Server.Domain.Entities;

public class FleetRule
{
    public int Id { get; set; }
    public int FleetId { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public bool IsPublic { get; set; }
    public string? Title { get; set; }
    public string? Description { get; set; }

    public Fleet Fleet { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
}
