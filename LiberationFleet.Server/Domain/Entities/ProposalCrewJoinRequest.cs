using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ProposalCrewJoinRequest
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int ApplicantUserId { get; set; }
    public string ApplicantUsername { get; set; } = string.Empty;
    public string AcceptedRuleIdsJson { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsApplied { get; set; }
    public bool IsKeyPrepared { get; set; }

    /// <summary>
    /// When approval happens while the applicant is already in another crew,
    /// set to <see cref="CrewJoinApplicantDecision.Pending"/> until they Switch or Stay.
    /// </summary>
    public CrewJoinApplicantDecision ApplicantDecision { get; set; }

    public Proposal Proposal { get; set; } = null!;
}
