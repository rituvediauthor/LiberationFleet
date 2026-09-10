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
    /// UI label: “Resolve on next vote”. Tied tallies stay pending after timer expiry until a
    /// later vote tips approve or reject. With exactly two eligible voters, settle early only
    /// once one side leads (author auto-approve alone does not settle early; on timer expiry a
    /// 1–0 lead still approves).
    /// </summary>
    ResolveOnFirstVote = 2
}
