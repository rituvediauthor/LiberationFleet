namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewStartSeason
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public int ReadyCountAtCreate { get; set; }
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
