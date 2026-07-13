using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.InviteCrewToFleet;

public record InviteCrewToFleetCommand(string JoinCode) : IRequest<InviteCrewToFleetResponse>;

public class InviteCrewToFleetCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository,
    IProposalRepository proposalRepository,
    CrewApplyToFleetProposalService crewApplyToFleetProposalService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<InviteCrewToFleetCommand, InviteCrewToFleetResponse>
{
    public async Task<InviteCrewToFleetResponse> Handle(
        InviteCrewToFleetCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "You must be in a crew to invite another crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var joinCode = (request.JoinCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "Join code is required." };
        }

        var crew = await crewRepository.GetByJoinCodeAsync(joinCode, cancellationToken);
        if (crew is null)
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "No crew found with that join code." };
        }

        if (crew.Id == membership.CrewId)
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "You cannot invite your own crew." };
        }

        if (await fleetRepository.GetFleetForCrewAsync(crew.Id, cancellationToken) is not null)
        {
            return new InviteCrewToFleetResponse { Success = false, Message = "That crew already belongs to a fleet." };
        }

        var result = await crewApplyToFleetProposalService.CreateAsync(
            currentUser.UserId.Value,
            crew.Id,
            fleet,
            fleet.JoinCode,
            Array.Empty<int>(),
            cancellationToken,
            initiatedByFleetInvite: true);

        if (!result.Success)
        {
            return new InviteCrewToFleetResponse
            {
                Success = false,
                Message = result.Message,
                ProposalId = result.ProposalId
            };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var pending = await proposalRepository.GetPendingCrewApplyToFleetAsync(crew.Id, fleet.Id, cancellationToken);
        var proposalId = pending?.ProposalId ?? result.ProposalId;

        await notificationService.NotifyCrewAsync(
            crew.Id,
            NotificationKind.NewProposal,
            "Fleet invitation",
            $"{fleet.Name} invited your crew to join their fleet.",
            $"/app/crew/proposals/{proposalId}",
            relatedEntityId: proposalId,
            excludeUserId: null,
            cancellationToken: cancellationToken);

        return new InviteCrewToFleetResponse
        {
            Success = true,
            Message = $"Invitation sent to {crew.Name}.",
            ProposalId = proposalId,
            CrewId = crew.Id,
            CrewName = crew.Name
        };
    }
}
