namespace LiberationFleet.Server.Domain.Entities;

public class ProposalFleetJoinRequest
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int FleetId { get; set; }
    public int ApplicantCrewId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
    public Fleet Fleet { get; set; } = null!;
    public Crew ApplicantCrew { get; set; } = null!;
}
