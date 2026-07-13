using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetLibraryStatus;

public record GetFleetLibraryStatusQuery : IRequest<FleetLibraryStatusDto>;

public class GetFleetLibraryStatusQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository) : IRequestHandler<GetFleetLibraryStatusQuery, FleetLibraryStatusDto>
{
    public async Task<FleetLibraryStatusDto> Handle(GetFleetLibraryStatusQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new FleetLibraryStatusDto { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new FleetLibraryStatusDto { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new FleetLibraryStatusDto { Success = false, Message = "Your crew is not in a fleet." };
        }

        return new FleetLibraryStatusDto
        {
            Success = true,
            Message = "Fleet library status loaded.",
            LibraryOfThingsEnabled = fleet.LibraryOfThingsEnabled,
            FleetId = fleet.Id
        };
    }
}
