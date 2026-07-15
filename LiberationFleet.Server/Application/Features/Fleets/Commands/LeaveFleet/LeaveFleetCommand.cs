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
    IUnitOfWork unitOfWork) : IRequestHandler<LeaveFleetCommand, FleetOperationResponse>
{
    public async Task<FleetOperationResponse> Handle(LeaveFleetCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!membership.IsOrganizer)
        {
            return new FleetOperationResponse
            {
                Success = false,
                Message = "Only an organizer can remove the crew from the fleet."
            };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var fleetCrew = await fleetRepository.GetFleetCrewAsync(fleet.Id, membership.CrewId, cancellationToken);
        if (fleetCrew is null)
        {
            return new FleetOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        await contentTenureService.OnCrewLeftFleetAsync(membership.CrewId, fleet.Id, cancellationToken);
        await fleetRepository.RemoveFleetCrewAsync(fleetCrew, cancellationToken);

        var room = await fleetRepository.GetLinkedFleetChatRoomAsync(fleet.Id, membership.CrewId, cancellationToken);
        if (room is not null)
        {
            room.IsDeleted = true;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetOperationResponse
        {
            Success = true,
            Message = "Your crew left the fleet."
        };
    }
}
