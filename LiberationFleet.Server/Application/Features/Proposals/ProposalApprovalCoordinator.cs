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
        CrewmateAidStatProposalService crewmateAidStatProposalService,
        CrewApplyToFleetProposalService crewApplyToFleetProposalService,
        CrewLeaveFleetProposalService crewLeaveFleetProposalService,
        CrewStartSeasonProposalService crewStartSeasonProposalService,
        FleetJoinRequestProposalService fleetJoinRequestProposalService,
        FleetKickCrewProposalService fleetKickCrewProposalService,
        FleetSettingsProposalService fleetSettingsProposalService,
        FleetRulesProposalService fleetRulesProposalService,
        CancellationToken cancellationToken)
    {
        // Always attempt apply when Approved (handlers no-op when already applied).
        // Do not gate on statusBefore: if approval was saved before a failed apply,
        // later proposal reads must still be able to finish applying.
        // (Call sites still use statusBefore for transition notifications.)
        _ = statusBefore;
        if (proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        // Apply must never fail a proposal GET/vote/timer read — unfinished work retries next time.
        await ApplyQuietly(() => crewSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewChatsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewmateKickProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewmateRejoinProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewJoinRequestProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewRoleProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => claimPlaceholderIdentityProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewmatePermissionProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewmateAidStatProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewApplyToFleetProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewLeaveFleetProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => crewStartSeasonProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => fleetJoinRequestProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => fleetKickCrewProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => fleetSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
        await ApplyQuietly(() => fleetRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken));
    }

    private static async Task ApplyQuietly(Func<Task> apply)
    {
        try
        {
            await apply();
        }
        catch (Exception)
        {
            // Retry on the next proposal read.
        }
    }
}
