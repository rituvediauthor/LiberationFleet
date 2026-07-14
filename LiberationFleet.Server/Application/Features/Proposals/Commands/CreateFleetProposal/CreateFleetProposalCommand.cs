using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.CreateFleetProposal;

public record CreateFleetProposalCommand(string Title, string Description) : IRequest<ProposalOperationResponse>;

public class CreateFleetProposalCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IGiftRepository giftRepository,
    IFleetRepository fleetRepository,
    IProposalRepository proposalRepository,
    NotificationService notificationService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFleetProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(
        CreateFleetProposalCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var title = request.Title?.Trim() ?? string.Empty;
        var description = request.Description?.Trim() ?? string.Empty;
        if (string.IsNullOrWhiteSpace(title) || string.IsNullOrWhiteSpace(description))
        {
            return new ProposalOperationResponse { Success = false, Message = "Title and description are required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Crew not found." };
        }

        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            userId,
            membership.CrewId,
            crew.CurrentSeasonStartDate,
            cancellationToken);
        var fleetTenureDays = await contentTenureService.GetFleetTenureDaysAsync(
            userId,
            fleet.Id,
            cancellationToken);

        if (!FleetContentPermissionService.CanCreateProposals(
                fleet,
                membership,
                giftStats.LifetimeContributions,
                fleetTenureDays))
        {
            return new ProposalOperationResponse
            {
                Success = false,
                Message = "You are not allowed to create proposals in this fleet yet."
            };
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            FleetId = fleet.Id,
            AuthorUserId = userId,
            Kind = ProposalKind.General,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };
        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);

        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await proposalRepository.AddFleetNoticeAsync(new ProposalFleetNotice
        {
            Proposal = proposal,
            Title = title,
            Description = description
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var fleetCrews = await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken);
        foreach (var fleetCrew in fleetCrews)
        {
            await notificationService.NotifyCrewAsync(
                fleetCrew.CrewId,
                NotificationKind.NewFleetProposal,
                "New fleet proposal",
                "A new fleet proposal was submitted.",
                $"/app/fleet/proposals/{proposal.Id}",
                relatedEntityId: proposal.Id,
                excludeUserId: userId,
                cancellationToken: cancellationToken);
        }

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Proposal created.",
            ProposalId = proposal.Id
        };
    }
}
