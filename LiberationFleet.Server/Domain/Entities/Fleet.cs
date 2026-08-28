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
    /// <summary>
    /// When the approval timer expires with equal approve/reject counts (any tie),
    /// and early settlement when exactly two eligible voters can vote.
    /// </summary>
    public DuoVoteTimeoutMode DuoVoteTimeoutMode { get; set; } = DuoVoteTimeoutMode.AutoReject;
    public bool AutoResolveOverTime { get; set; } = true;
    public int BaseAutoResolveHours { get; set; } = 24;
    public bool ChangeAutoResolveTimerOnFirstReject { get; set; } = true;
    public int AutoResolveHoursAfterFirstReject { get; set; } = 168;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }
    public string? ImageResourceId { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<FleetCrew> Crews { get; set; } = new List<FleetCrew>();
}
