namespace LiberationFleet.Server.Domain.Entities;

public class ProposalFleetKickCrew
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int TargetCrewId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public string? Reason { get; set; }
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
    public Crew TargetCrew { get; set; } = null!;
}
