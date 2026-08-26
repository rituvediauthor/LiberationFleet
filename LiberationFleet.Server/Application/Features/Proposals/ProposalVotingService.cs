using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalVotingService
{
    /// <summary>
    /// Votes needed to resolve early. Two eligible voters require unanimous approval (100%).
    /// Larger crews use ceil(N × 0.5).
    /// </summary>
    public static int RequiredApproveVotes(int eligibleVoterCount) =>
        eligibleVoterCount <= 0
            ? int.MaxValue
            : eligibleVoterCount == 2
                ? 2
                : (int)Math.Ceiling(eligibleVoterCount * 0.5);

    /// <summary>
    /// Votes needed to reject early. For two eligible voters, a single disapproval makes
    /// unanimous approval impossible, so one disapproval rejects. Otherwise same threshold as approve.
    /// </summary>
    public static int RequiredRejectVotes(int eligibleVoterCount) =>
        eligibleVoterCount <= 0
            ? int.MaxValue
            : eligibleVoterCount == 2
                ? 1
                : (int)Math.Ceiling(eligibleVoterCount * 0.5);

    [Obsolete("Use RequiredApproveVotes")]
    public static int RequiredVotesForMajority(int eligibleVoterCount) =>
        RequiredApproveVotes(eligibleVoterCount);

    public static void ApplyTimerRulesOnCreate(Proposal proposal, DateTime utcNow)
    {
        proposal.Status = ProposalStatus.Pending;
        proposal.ApprovalTimerEndsAt = utcNow.AddHours(24);
    }

    /// <summary>Record the submitter's automatic approve vote once the proposal has an Id.</summary>
    public static async Task EnsureAuthorApproveVoteAsync(
        IProposalRepository proposalRepository,
        Proposal proposal,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var existing = await proposalRepository.GetVoteAsync(proposal.Id, proposal.AuthorUserId, cancellationToken);
        if (existing is not null)
        {
            return;
        }

        await proposalRepository.AddVoteAsync(new ProposalVote
        {
            ProposalId = proposal.Id,
            UserId = proposal.AuthorUserId,
            IsApprove = true,
            VotedAt = utcNow
        }, cancellationToken);
        proposal.ApproveCount++;
    }

    public static async Task RecalculateAfterAuthorVoteAsync(
        Proposal proposal,
        IProposalRepository proposalRepository,
        IFleetRepository fleetRepository,
        ICrewRepository crewRepository,
        DateTime utcNow,
        CancellationToken cancellationToken)
    {
        var eligibleCount = await ProposalEligibility.GetEligibleVoterCountAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            cancellationToken);
        var duoMode = await ProposalEligibility.GetDuoVoteTimeoutModeAsync(
            proposal,
            crewRepository,
            fleetRepository,
            cancellationToken);
        RecalculateStatus(proposal, eligibleCount, utcNow, duoMode);
    }

    public static void ApplyDisapproveTimerExtension(Proposal proposal, DateTime utcNow)
    {
        proposal.ApprovalTimerEndsAt = utcNow.AddDays(7);
    }

    public static void RecalculateStatus(
        Proposal proposal,
        int eligibleVoterCount,
        DateTime utcNow,
        DuoVoteTimeoutMode duoMode = DuoVoteTimeoutMode.AutoReject)
    {
        // Duo ResolveOnFirstVote: author auto-approve alone must not settle.
        // A disapproval or a second approval settles immediately (matches duo thresholds).
        if (eligibleVoterCount == 2 && duoMode == DuoVoteTimeoutMode.ResolveOnFirstVote)
        {
            if (proposal.DisapproveCount > 0)
            {
                proposal.Status = ProposalStatus.Rejected;
                proposal.ApprovalTimerEndsAt = null;
                return;
            }

            if (proposal.ApproveCount >= 2)
            {
                proposal.Status = ProposalStatus.Approved;
                proposal.ApprovalTimerEndsAt = null;
                return;
            }
        }
        else
        {
            var requiredReject = RequiredRejectVotes(eligibleVoterCount);
            if (proposal.DisapproveCount >= requiredReject)
            {
                proposal.Status = ProposalStatus.Rejected;
                proposal.ApprovalTimerEndsAt = null;
                return;
            }

            var requiredApprove = RequiredApproveVotes(eligibleVoterCount);
            if (proposal.ApproveCount >= requiredApprove)
            {
                proposal.Status = ProposalStatus.Approved;
                proposal.ApprovalTimerEndsAt = null;
                return;
            }
        }

        // Two eligible voters at 1–1: split rejects immediately.
        if (eligibleVoterCount == 2
            && proposal.ApproveCount == 1
            && proposal.DisapproveCount == 1)
        {
            proposal.Status = ProposalStatus.Rejected;
            proposal.ApprovalTimerEndsAt = null;
            return;
        }

        if (proposal.Status is ProposalStatus.Approved or ProposalStatus.Rejected)
        {
            proposal.Status = ProposalStatus.Pending;
            if (!proposal.ApprovalTimerEndsAt.HasValue)
            {
                proposal.ApprovalTimerEndsAt = proposal.DisapproveCount > 0
                    ? utcNow.AddDays(7)
                    : utcNow.AddHours(24);
            }
        }

        TryResolveOnTimer(proposal, utcNow, duoMode, eligibleVoterCount);
    }

    /// <summary>
    /// Resolve a still-pending proposal once UtcNow is at or past ApprovalTimerEndsAt.
    /// Cast-vote majority wins (approve count &gt; disapprove → approve, and vice versa).
    /// Ties (including 0–0) reject, except two-voter incomplete outcomes which follow DuoVoteTimeoutMode.
    /// </summary>
    public static void TryResolveOnTimer(
        Proposal proposal,
        DateTime utcNow,
        DuoVoteTimeoutMode duoMode = DuoVoteTimeoutMode.AutoReject,
        int? eligibleVoterCount = null)
    {
        if (proposal.Status != ProposalStatus.Pending
            || !proposal.ApprovalTimerEndsAt.HasValue
            || proposal.ApprovalTimerEndsAt.Value > utcNow)
        {
            return;
        }

        if (eligibleVoterCount == 2
            && duoMode is DuoVoteTimeoutMode.AutoApprove or DuoVoteTimeoutMode.AutoReject
            && proposal.ApproveCount + proposal.DisapproveCount < 2)
        {
            // Incomplete duo vote: honor AutoApprove / AutoReject timeout preference.
            // ResolveOnFirstVote falls through to cast-vote majority (author-only 1–0 → approve).
            proposal.Status = duoMode == DuoVoteTimeoutMode.AutoApprove
                ? ProposalStatus.Approved
                : ProposalStatus.Rejected;
            proposal.ApprovalTimerEndsAt = null;
            return;
        }

        if (proposal.ApproveCount > proposal.DisapproveCount)
        {
            proposal.Status = ProposalStatus.Approved;
        }
        else
        {
            // Majority rejection, or tie / no votes → reject.
            proposal.Status = ProposalStatus.Rejected;
        }

        proposal.ApprovalTimerEndsAt = null;
    }
}
