using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetDurableLibraryUnits;

public record GetDurableLibraryUnitsQuery(
    string? Search,
    IReadOnlyList<int> CategoryIds,
    int Limit = 30,
    int Offset = 0) : IRequest<LibraryUnitListResponse>;

public class GetDurableLibraryUnitsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetDurableLibraryUnitsQuery, LibraryUnitListResponse>
{
    public async Task<LibraryUnitListResponse> Handle(
        GetDurableLibraryUnitsQuery request,
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

        var crewIds = await LibraryScopeHelper.GetAccessibleCrewIdsAsync(
            membership.CrewId,
            fleetRepository,
            cancellationToken);

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
            Message = "Durable goods loaded.",
            Items = page.Items.Select(LibraryMapper.MapUnitListItem).ToList(),
            HasMore = page.HasMore
        };
    }
}
