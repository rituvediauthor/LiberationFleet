namespace LiberationFleet.Server.Domain.Enums;

/// <summary>
/// How pending proposals resolve when the approval timer expires on an equal
/// approve/reject tally (any tie, including 0–0). Also controls early settlement
/// when exactly two eligible voters can vote (see <see cref="ResolveOnFirstVote"/>).
/// </summary>
public enum DuoVoteTimeoutMode
{
    /// <summary>On timer expiry with equal approve/reject counts, approve.</summary>
    AutoApprove = 0,

    /// <summary>On timer expiry with equal approve/reject counts, reject (default).</summary>
    AutoReject = 1,

    /// <summary>
    /// When exactly two eligible voters can vote: one reject rejects immediately;
    /// a second approve approves immediately (author auto-approve alone does not settle).
    /// Timer expiry ties still follow AutoReject.
    /// </summary>
    ResolveOnFirstVote = 2
}
