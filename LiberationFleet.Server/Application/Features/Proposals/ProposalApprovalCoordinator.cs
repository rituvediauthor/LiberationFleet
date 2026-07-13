using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Fleets;
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
        CrewChatsProposalService crewChatsProposalService,
        CrewmateKickProposalService crewmateKickProposalService,
        CrewmateRejoinProposalService crewmateRejoinProposalService,
        CrewJoinRequestProposalService crewJoinRequestProposalService,
        CrewRoleProposalService crewRoleProposalService,
        ClaimPlaceholderIdentityProposalService claimPlaceholderIdentityProposalService,
        CrewmatePermissionProposalService crewmatePermissionProposalService,
        CrewApplyToFleetProposalService crewApplyToFleetProposalService,
        FleetJoinRequestProposalService fleetJoinRequestProposalService,
        FleetKickCrewProposalService fleetKickCrewProposalService,
        FleetSettingsProposalService fleetSettingsProposalService,
        FleetRulesProposalService fleetRulesProposalService,
        CancellationToken cancellationToken)
    {
        if (statusBefore == ProposalStatus.Approved || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        await crewSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewChatsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewmateKickProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewmateRejoinProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewJoinRequestProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewRoleProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await claimPlaceholderIdentityProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewmatePermissionProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await crewApplyToFleetProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await fleetJoinRequestProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await fleetKickCrewProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await fleetSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
        await fleetRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken);
    }
}
