using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.LeaveFleet;

public record LeaveFleetCommand : IRequest<FleetOperationResponse>;

public class LeaveFleetCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ContentTenureService contentTenureService,
    CrewLeaveFleetProposalService crewLeaveFleetProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<LeaveFleetCommand, FleetOperationResponse>
{
    public async Task<FleetOperationResponse> Handle(LeaveFleetCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            var noCrewMembership = await fleetRepository.GetFleetMembershipForUserAsync(userId, cancellationToken);
            if (noCrewMembership is null)
            {
                return new FleetOperationResponse { Success = false, Message = "You are not in a fleet." };
            }

            await contentTenureService.PauseFleetAsync(userId, noCrewMembership.FleetId, cancellationToken);
            await fleetRepository.RemoveFleetMembershipAsync(noCrewMembership, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new FleetOperationResponse
            {
                Success = true,
                Message = "You left the fleet."
            };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var proposalResult = await crewLeaveFleetProposalService.CreateAsync(
            userId,
            membership.CrewId,
            fleet.Id,
            cancellationToken);

        return new FleetOperationResponse
        {
            Success = proposalResult.Success,
            Message = proposalResult.Message,
            ProposalsSubmitted = proposalResult.Success,
            ProposalsCreated = proposalResult.Success ? 1 : 0
        };
    }
}
