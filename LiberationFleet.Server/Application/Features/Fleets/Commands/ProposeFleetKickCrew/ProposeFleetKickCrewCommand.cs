using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.ProposeFleetKickCrew;

public record ProposeFleetKickCrewCommand(int TargetCrewId, string? Reason)
    : IRequest<FleetJoinRequestOperationResponse>;

public class ProposeFleetKickCrewCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    FleetKickCrewProposalService kickCrewProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<ProposeFleetKickCrewCommand, FleetJoinRequestOperationResponse>
{
    public async Task<FleetJoinRequestOperationResponse> Handle(
        ProposeFleetKickCrewCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetJoinRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetJoinRequestOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetJoinRequestOperationResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var result = await kickCrewProposalService.CreateAsync(
            fleet.Id,
            currentUser.UserId.Value,
            request.TargetCrewId,
            request.Reason,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new FleetJoinRequestOperationResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
