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

        // One matching apply instead of probing every handler on each list row.
        Func<Task> apply = proposal.Kind switch
        {
            ProposalKind.CrewSettingChange => () => crewSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewRuleChange => () => crewRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewChatChange or ProposalKind.FleetChatChange =>
                () => crewChatsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewmateKick or ProposalKind.CrewmateSeasonKick =>
                () => crewmateKickProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewmateRejoin => () => crewmateRejoinProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewJoinRequest => () => crewJoinRequestProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewRoleChange => () => crewRoleProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.ClaimPlaceholderIdentity =>
                () => claimPlaceholderIdentityProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewmatePermissionGrant =>
                () => crewmatePermissionProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewmateAidStatChange =>
                () => crewmateAidStatProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewApplyToFleet => () => crewApplyToFleetProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewLeaveFleet => () => crewLeaveFleetProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.CrewStartSeason => () => crewStartSeasonProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.FleetJoinRequest => () => fleetJoinRequestProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.FleetKickCrew => () => fleetKickCrewProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.FleetSettingChange => () => fleetSettingsProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            ProposalKind.FleetRuleChange => () => fleetRulesProposalService.TryApplyApprovedProposalAsync(proposal, cancellationToken),
            _ => () => Task.CompletedTask
        };
        await ApplyQuietly(apply);
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
