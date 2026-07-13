using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ProposalFleetSettingChange
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public FleetSettingField Field { get; set; }
    public string NewValue { get; set; } = string.Empty;
    public string Title { get; set; } = "Editing fleet settings";
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
