using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalVotingService
{
    public static int RequiredVotesForMajority(int eligibleVoterCount) =>
        eligibleVoterCount <= 0 ? 1 : (int)Math.Ceiling(eligibleVoterCount * 0.5);

    public static void ApplyTimerRulesOnCreate(Proposal proposal, DateTime utcNow)
    {
        proposal.Status = ProposalStatus.Pending;
        proposal.ApprovalTimerEndsAt = utcNow.AddHours(24);
    }

    public static void ApplyDisapproveTimerExtension(Proposal proposal, DateTime utcNow)
    {
        proposal.ApprovalTimerEndsAt = utcNow.AddDays(7);
    }

    public static void RecalculateStatus(Proposal proposal, int eligibleVoterCount, DateTime utcNow)
    {
        var required = RequiredVotesForMajority(eligibleVoterCount);

        if (proposal.DisapproveCount >= required)
        {
            proposal.Status = ProposalStatus.Rejected;
            proposal.ApprovalTimerEndsAt = null;
            return;
        }

        if (proposal.ApproveCount >= required)
        {
            proposal.Status = ProposalStatus.Approved;
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

        TryAutoApproveOnTimer(proposal, utcNow);
    }

    public static void TryAutoApproveOnTimer(Proposal proposal, DateTime utcNow)
    {
        if (proposal.Status != ProposalStatus.Pending
            || !proposal.ApprovalTimerEndsAt.HasValue
            || proposal.ApprovalTimerEndsAt > utcNow
            || proposal.DisapproveCount > 0)
        {
            return;
        }

        proposal.Status = ProposalStatus.Approved;
        proposal.ApprovalTimerEndsAt = null;
    }
}
