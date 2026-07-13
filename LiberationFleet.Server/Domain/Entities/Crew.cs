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
    public bool AllowSurvivalThresholds { get; set; } = true;
    public bool RequireApprovalForEdits { get; set; } = true;
    public decimal InNeedDefaultThreshold { get; set; } = 20m;
    public bool LibraryOfThingsEnabled { get; set; } = true;
    public CycleCapMode MemberCycleCapMode { get; set; } = CycleCapMode.CapacityBased;
    public decimal MemberCycleCapFixedAmount { get; set; }
    public decimal MemberCycleCapMultiplier { get; set; } = 2m;
    public CycleCapMode NonMemberCycleCapMode { get; set; } = CycleCapMode.CapacityBased;
    public decimal NonMemberCycleCapFixedAmount { get; set; }
    public decimal NonMemberCycleCapMultiplier { get; set; } = 0.5m;
    public bool AllowCrewmateFileAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForAttachments { get; set; }
    public decimal MinimumContributionForAttachments { get; set; }
    public int MinimumCrewmateTenureDaysForProposals { get; set; }
    public decimal MinimumContributionForProposals { get; set; }

    public User CreatedByUser { get; set; } = null!;
    public ICollection<CrewMembership> Memberships { get; set; } = new List<CrewMembership>();
}
