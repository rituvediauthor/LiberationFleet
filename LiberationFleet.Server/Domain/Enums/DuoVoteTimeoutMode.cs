namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// How pending proposals behave when exactly two eligible voters exist and the
/// outcome is still unresolved when the approval timer expires (or on first vote).
/// </summary>
public enum DuoVoteTimeoutMode
{
    /// <summary>On timer expiry with incomplete votes, approve.</summary>
    AutoApprove = 0,

    /// <summary>On timer expiry with incomplete votes, reject (default).</summary>
    AutoReject = 1,

    /// <summary>First approve or disapprove vote immediately decides the proposal.</summary>
    ResolveOnFirstVote = 2
}
