namespace LiberationFleet.Server.Domain.Entities;

public class CrewMembership
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CrewId { get; set; }
    public bool IsBanned { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public bool IsOrganizer { get; set; }
    public bool IsHonoraryMember { get; set; }
    public bool IsAdvocate { get; set; }
    public bool IsDecentralizer { get; set; }
    public bool IsCeremonialOrganizer { get; set; }
    public bool IsModerator { get; set; }
    public bool IsIntermediary { get; set; }
    public int IntermediaryFailedCompletions { get; set; }
    public int EmergencySacrificesThisSeason { get; set; }
    public bool IsPlaceholderMember { get; set; }
    public bool CanAttachFiles { get; set; }
    public bool CanCreateProposals { get; set; }
    public decimal? EstimatedMonthlyContribution { get; set; }
    public bool IsSeasonReady { get; set; }
    public bool IsInSeason { get; set; }
    public decimal CurrentPriorityScore { get; set; }

    public User User { get; set; } = null!;
    public Crew Crew { get; set; } = null!;
}
