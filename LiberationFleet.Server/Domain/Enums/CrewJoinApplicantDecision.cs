namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// Applicant choice when a join request is approved while they already belong to another crew.
/// </summary>
public enum CrewJoinApplicantDecision
{
    None = 0,
    Pending = 1,
    Switched = 2,
    Stayed = 3
}
