using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Rules;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.VoteProposal;

public record VoteProposalCommand(int ProposalId, string Vote) : IRequest<ProposalOperationResponse>;

public class VoteProposalCommandHandler(
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
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<VoteProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(VoteProposalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var isApprove = request.Vote.Equals("approve", StringComparison.OrdinalIgnoreCase);
        var isDisapprove = request.Vote.Equals("disapprove", StringComparison.OrdinalIgnoreCase);
        if (!isApprove && !isDisapprove)
        {
            return new ProposalOperationResponse { Success = false, Message = "Vote must be approve or disapprove." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        var (allowed, accessError) = await ProposalEligibility.CanUserAccessProposalAsync(
            userId,
            proposal,
            membershipRepository,
            fleetRepository,
            cancellationToken);
        if (!allowed)
        {
            return new ProposalOperationResponse { Success = false, Message = accessError ?? "Access denied." };
        }

        if (proposal.Kind is ProposalKind.CrewmateKick or ProposalKind.CrewmateSeasonKick)
        {
            var kick = await proposalRepository.GetCrewmateKickByProposalIdAsync(proposal.Id, cancellationToken);
            if (kick is not null && kick.TargetUserId == userId)
            {
                return new ProposalOperationResponse
                {
                    Success = false,
                    Message = proposal.Kind == ProposalKind.CrewmateSeasonKick
                        ? "You cannot vote on a proposal to remove you from the season."
                        : "You cannot vote on a proposal to kick you from the crew."
                };
            }
        }

        var utcNow = DateTime.UtcNow;
        var existingVote = await proposalRepository.GetVoteAsync(proposal.Id, userId, cancellationToken);

        if (existingVote is not null)
        {
            if (existingVote.IsApprove == isApprove)
            {
                return new ProposalOperationResponse { Success = true, Message = "Vote unchanged." };
            }

            if (existingVote.IsApprove)
            {
                proposal.ApproveCount = Math.Max(0, proposal.ApproveCount - 1);
            }
            else
            {
                proposal.DisapproveCount = Math.Max(0, proposal.DisapproveCount - 1);
            }

            proposalRepository.RemoveVote(existingVote);
        }

        await proposalRepository.AddVoteAsync(new ProposalVote
        {
            ProposalId = proposal.Id,
            UserId = userId,
            IsApprove = isApprove,
            VotedAt = utcNow
        }, cancellationToken);

        if (isApprove)
        {
            proposal.ApproveCount++;
        }
        else
        {
            proposal.DisapproveCount++;
            ProposalVotingService.ApplyDisapproveTimerExtension(proposal, utcNow);
        }

        proposal.LastActivityAt = utcNow;
        var eligibleCount = await ProposalEligibility.GetEligibleVoterCountAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            cancellationToken);
        var statusBefore = proposal.Status;
        ProposalVotingService.RecalculateStatus(proposal, eligibleCount, utcNow);
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (statusBefore != proposal.Status)
        {
            if (proposal.Status == ProposalStatus.Approved)
            {
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = proposal.AuthorUserId,
                    CrewId = proposal.CrewId,
                    Kind = NotificationKind.ProposalAccepted,
                    Title = "Proposal accepted",
                    Body = proposal.FleetId.HasValue
                        ? "Your fleet proposal was approved."
                        : "Your crew proposal was approved.",
                    ActionUrl = $"/app/crew/proposals/{proposal.Id}",
                    RelatedEntityId = proposal.Id
                }, cancellationToken);
            }
            else if (proposal.Status == ProposalStatus.Rejected)
            {
                await notificationService.NotifyUserAsync(new CreateNotificationRequest
                {
                    UserId = proposal.AuthorUserId,
                    CrewId = proposal.CrewId,
                    Kind = NotificationKind.ProposalRejected,
                    Title = "Proposal rejected",
                    Body = proposal.FleetId.HasValue
                        ? "Your fleet proposal was rejected."
                        : "Your crew proposal was rejected.",
                    ActionUrl = $"/app/crew/proposals/list/rejected",
                    RelatedEntityId = proposal.Id
                }, cancellationToken);
            }
        }

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Vote recorded."
        };
    }
}
