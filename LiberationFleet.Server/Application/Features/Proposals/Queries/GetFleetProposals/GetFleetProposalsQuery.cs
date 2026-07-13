using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Queries.GetFleetProposals;

public record GetFleetProposalsQuery(string Status) : IRequest<ProposalListResponse>;

public class GetFleetProposalsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IProposalRepository proposalRepository,
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
    IUserBlockRepository blockRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<GetFleetProposalsQuery, ProposalListResponse>
{
    public async Task<ProposalListResponse> Handle(GetFleetProposalsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProposalListResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new ProposalListResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var status = ProposalMapper.ParseStatus(request.Status);
        var proposals = await proposalRepository.GetByFleetAndStatusAsync(fleet.Id, status, cancellationToken);
        var utcNow = DateTime.UtcNow;

        foreach (var proposal in proposals)
        {
            var statusBefore = proposal.Status;
            ProposalVotingService.TryAutoApproveOnTimer(proposal, utcNow);
            await ProposalApprovalCoordinator.ProcessNewlyApprovedAsync(
                proposal,
                statusBefore,
                crewSettingsProposalService,
                crewRulesProposalService,
                crewChatsProposalService,
                crewmateKickProposalService,
                crewmateRejoinProposalService,
                crewJoinRequestProposalService,
                crewRoleProposalService,
                claimPlaceholderIdentityProposalService,
                crewmatePermissionProposalService,
                crewApplyToFleetProposalService,
                fleetJoinRequestProposalService,
                fleetKickCrewProposalService,
                fleetSettingsProposalService,
                fleetRulesProposalService,
                cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        proposals = proposals
            .Where(p => ProposalMapper.IsVisibleDespiteBlock(p.Kind) || !hiddenUserIds.Contains(p.AuthorUserId))
            .ToList();

        var fleetSettingChanges = await proposalRepository.GetFleetSettingChangesByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.FleetSettingChange).Select(p => p.Id),
            cancellationToken);
        var fleetJoinRequests = await proposalRepository.GetFleetJoinRequestsByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.FleetJoinRequest).Select(p => p.Id),
            cancellationToken);
        var fleetKickCrews = await proposalRepository.GetFleetKickCrewsByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.FleetKickCrew).Select(p => p.Id),
            cancellationToken);
        var fleetRuleChanges = await proposalRepository.GetFleetRuleChangesByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.FleetRuleChange).Select(p => p.Id),
            cancellationToken);
        var fleetNotices = await proposalRepository.GetFleetNoticesByProposalIdsAsync(
            proposals.Where(p => p.Kind == ProposalKind.General).Select(p => p.Id),
            cancellationToken);

        var items = new List<ProposalListItemDto>();
        foreach (var proposal in proposals)
        {
            fleetSettingChanges.TryGetValue(proposal.Id, out var fleetSettingChange);
            fleetJoinRequests.TryGetValue(proposal.Id, out var fleetJoinRequest);
            fleetKickCrews.TryGetValue(proposal.Id, out var fleetKickCrew);
            fleetRuleChanges.TryGetValue(proposal.Id, out var fleetRuleChange);
            fleetNotices.TryGetValue(proposal.Id, out var fleetNotice);
            var vote = await proposalRepository.GetVoteAsync(proposal.Id, userId, cancellationToken);
            var currentUserVote = vote is null ? null : vote.IsApprove ? "approve" : "disapprove";
            items.Add(ProposalMapper.MapListItem(
                proposal,
                envelope: null,
                currentUserVote: currentUserVote,
                fleetRuleChange: fleetRuleChange,
                fleetSettingChange: fleetSettingChange,
                fleetJoinRequest: fleetJoinRequest,
                fleetKickCrew: fleetKickCrew,
                fleetNotice: fleetNotice));
        }

        return new ProposalListResponse
        {
            Success = true,
            Message = "Fleet proposals loaded.",
            Items = items
        };
    }
}
