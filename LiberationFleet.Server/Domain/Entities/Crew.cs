using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class Crew
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int MaxSize { get; set; }
    public CrewPrivacy Privacy { get; set; }
    public CrewScope Scope { get; set; }
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool SeasonStarted { get; set; }
    public DateTime? CurrentSeasonStartDate { get; set; }
    public decimal SeasonMemberCycleCap { get; set; }
    public decimal SeasonNonMemberCycleCap { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<CrewMembership> Memberships { get; set; } = new List<CrewMembership>();
}
