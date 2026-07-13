namespace LiberationFleet.Server.Domain.Entities;

public class ProposalFleetNotice
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;

    public Proposal Proposal { get; set; } = null!;
}
