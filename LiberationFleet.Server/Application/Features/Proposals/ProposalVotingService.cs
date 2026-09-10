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

    public static void ApplyTimerRulesOnCreate(
        Proposal proposal,
        DateTime utcNow,
        ProposalAutoResolveSettings settings)
    {
        proposal.Status = ProposalStatus.Pending;
        proposal.ApprovalTimerEndsAt = settings.ComputeTimerEnd(utcNow, disapproveCount: 0);
    }

    public static async Task ApplyTimerRulesOnCreateAsync(
        Proposal proposal,
        DateTime utcNow,
        ICrewRepository crewRepository,
        IFleetRepository fleetRepository,
        CancellationToken cancellationToken)
    {
        var settings = await ProposalEligibility.GetAutoResolveSettingsAsync(
            proposal,
            crewRepository,
            fleetRepository,
            cancellationToken);
        ApplyTimerRulesOnCreate(proposal, utcNow, settings);
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
        var autoResolveSettings = await ProposalEligibility.GetAutoResolveSettingsAsync(
            proposal,
            crewRepository,
            fleetRepository,
            cancellationToken);
        RecalculateStatus(proposal, eligibleCount, utcNow, duoMode, autoResolveSettings);
    }

    public static void ApplyDisapproveTimerExtension(
        Proposal proposal,
        DateTime utcNow,
        ProposalAutoResolveSettings settings)
    {
        if (!settings.AutoResolveOverTime
            || !settings.ChangeAutoResolveTimerOnFirstReject
            || proposal.DisapproveCount != 1)
        {
            return;
        }

        proposal.ApprovalTimerEndsAt = utcNow.AddHours(settings.AutoResolveHoursAfterFirstReject);
    }

    public static void RecalculateStatus(
        Proposal proposal,
        int eligibleVoterCount,
        DateTime utcNow,
        DuoVoteTimeoutMode duoMode = DuoVoteTimeoutMode.AutoReject,
        ProposalAutoResolveSettings? autoResolveSettings = null)
    {
        var settings = autoResolveSettings ?? ProposalAutoResolveSettings.Defaults;

        // Duo "resolve on next vote": settle only once one side leads.
        // Author auto-approve alone (1–0) does not settle early; timer expiry still approves a lead.
        if (eligibleVoterCount == 2 && duoMode == DuoVoteTimeoutMode.ResolveOnFirstVote)
        {
            if (proposal.DisapproveCount > proposal.ApproveCount)
            {
                proposal.Status = ProposalStatus.Rejected;
                proposal.ApprovalTimerEndsAt = null;
                return;
            }

            if (proposal.ApproveCount > proposal.DisapproveCount && proposal.ApproveCount >= 2)
            {
                proposal.Status = ProposalStatus.Approved;
                proposal.ApprovalTimerEndsAt = null;
                return;
            }

            // Tied (including 1–1) or author-only approve: stay pending for timer / next vote.
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

        // Two eligible voters at 1–1: AutoApprove / AutoReject settle immediately.
        // Resolve-on-next-vote holds the tie until a later vote tips it (or timer path).
        if (eligibleVoterCount == 2
            && proposal.ApproveCount == 1
            && proposal.DisapproveCount == 1)
        {
            if (duoMode == DuoVoteTimeoutMode.ResolveOnFirstVote)
            {
                proposal.Status = ProposalStatus.Pending;
                TryResolveOnTimer(proposal, utcNow, duoMode, settings, eligibleVoterCount);
                return;
            }

            proposal.Status = duoMode == DuoVoteTimeoutMode.AutoApprove
                ? ProposalStatus.Approved
                : ProposalStatus.Rejected;
            proposal.ApprovalTimerEndsAt = null;
            return;
        }

        if (proposal.Status is ProposalStatus.Approved or ProposalStatus.Rejected)
        {
            proposal.Status = ProposalStatus.Pending;
            if (!proposal.ApprovalTimerEndsAt.HasValue)
            {
                proposal.ApprovalTimerEndsAt = settings.ComputeTimerEnd(utcNow, proposal.DisapproveCount);
            }
        }

        TryResolveOnTimer(proposal, utcNow, duoMode, settings, eligibleVoterCount);
    }

    /// <summary>
    /// Resolve a still-pending proposal once UtcNow is at or past ApprovalTimerEndsAt.
    /// Cast-vote majority wins (approve count &gt; disapprove → approve, and vice versa).
    /// Equal tallies: AutoApprove / AutoReject pick a side; ResolveOnFirstVote (next-vote mode)
    /// leaves the proposal pending until a later vote tips the tally.
    /// </summary>
    public static void TryResolveOnTimer(
        Proposal proposal,
        DateTime utcNow,
        DuoVoteTimeoutMode duoMode = DuoVoteTimeoutMode.AutoReject,
        ProposalAutoResolveSettings? autoResolveSettings = null,
        int? eligibleVoterCount = null)
    {
        var settings = autoResolveSettings ?? ProposalAutoResolveSettings.Defaults;
        if (!settings.AutoResolveOverTime
            || proposal.Status != ProposalStatus.Pending
            || !proposal.ApprovalTimerEndsAt.HasValue
            || proposal.ApprovalTimerEndsAt.Value > utcNow)
        {
            return;
        }

        if (proposal.ApproveCount == proposal.DisapproveCount)
        {
            if (duoMode == DuoVoteTimeoutMode.ResolveOnFirstVote)
            {
                // Hold ties (including 0–0 / 1–1) until a later vote creates a lead.
                proposal.ApprovalTimerEndsAt = null;
                return;
            }

            proposal.Status = duoMode == DuoVoteTimeoutMode.AutoApprove
                ? ProposalStatus.Approved
                : ProposalStatus.Rejected;
            proposal.ApprovalTimerEndsAt = null;
            return;
        }

        // Includes author auto-approve alone (1–0): resolve toward approval.
        if (proposal.ApproveCount > proposal.DisapproveCount)
        {
            proposal.Status = ProposalStatus.Approved;
        }
        else
        {
            proposal.Status = ProposalStatus.Rejected;
        }

        proposal.ApprovalTimerEndsAt = null;
    }
}
