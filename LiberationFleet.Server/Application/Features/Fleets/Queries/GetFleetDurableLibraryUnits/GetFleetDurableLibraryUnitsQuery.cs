using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetDurableLibraryUnits;

public record GetFleetDurableLibraryUnitsQuery(
    string? Search,
    IReadOnlyList<int> CategoryIds,
    int Limit = 30,
    int Offset = 0) : IRequest<LibraryUnitListResponse>;

public class GetFleetDurableLibraryUnitsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetFleetDurableLibraryUnitsQuery, LibraryUnitListResponse>
{
    public async Task<LibraryUnitListResponse> Handle(
        GetFleetDurableLibraryUnitsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryUnitListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryUnitListResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new LibraryUnitListResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        if (!fleet.LibraryOfThingsEnabled)
        {
            return new LibraryUnitListResponse { Success = false, Message = "Fleet library is disabled." };
        }

        var crewIds = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken))
            .Select(fc => fc.CrewId)
            .ToList();

        var page = await libraryRepository.GetDurableUnitsForCrewIdsAsync(
            crewIds,
            request.Search,
            request.CategoryIds,
            Math.Clamp(request.Limit, 1, 100),
            Math.Max(request.Offset, 0),
            cancellationToken);

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Fleet durable goods loaded.",
            Items = page.Items.Select(LibraryMapper.MapUnitListItem).ToList(),
            HasMore = page.HasMore
        };
    }
}
