namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewmateAidStatChange
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int TargetUserId { get; set; }
    /// <summary>JSON array of { field, newValue } entries.</summary>
    public string ChangesJson { get; set; } = "[]";
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
