using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class Fleet
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public CrewPrivacy Privacy { get; set; }
    public CrewScope Scope { get; set; }
    public string? ZipCode { get; set; }
    public int? RadiusMiles { get; set; }
    public string JoinCode { get; set; } = string.Empty;
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool RequireApprovalForEdits { get; set; } = true;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<FleetCrew> Crews { get; set; } = new List<FleetCrew>();
}
