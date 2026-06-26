using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Proposals;

public static class ProposalApprovalCoordinator
{
    public static async Task ProcessNewlyApprovedAsync(
        Proposal proposal,
        ProposalStatus statusBefore,
        CrewSettingsProposalService crewSettingsProposalService,
        CrewRulesProposalService crewRulesProposalService,
        CancellationToken cancellationToken)
    {
        if (statusBefore == ProposalStatus.Approved || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        await crewSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
    }
}
