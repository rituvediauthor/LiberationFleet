using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
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
    ILibraryRepository libraryRepository,
    IViewerLocationAccessor viewerLocation) : IRequestHandler<GetDurableLibraryUnitsQuery, LibraryUnitListResponse>
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

        var viewerCountry = viewerLocation.CountryCode;
        var viewerZip = viewerLocation.ZipCode;

        var fetchLimit = Math.Clamp(request.Limit, 1, 100);
        var fetchOffset = Math.Max(request.Offset, 0);
        var page = await libraryRepository.GetDurableUnitsForCrewIdsAsync(
            crewIds,
            membership.CrewId,
            request.Search,
            request.CategoryIds,
            Math.Min(100, fetchLimit * 3),
            fetchOffset,
            cancellationToken);

        var now = DateTime.UtcNow;
        var items = page.Items
            .Where(unit => LibraryOfferingRules.IsVisibleToViewerZip(unit.Offering, viewerCountry, viewerZip))
            .Take(fetchLimit)
            .Select(unit =>
            {
                var dto = LibraryMapper.MapUnitListItem(unit);
                var openWindows = unit.Requests
                    .Where(r => r.Status == LibraryRequestStatus.Open)
                    .ToList();
                var reservedNow = openWindows.Any(r => r.NeededByStart <= now && r.NeededByEnd >= now);
                dto.AvailableNow = !reservedNow;
                dto.NextAvailableDate = reservedNow
                    ? openWindows
                        .Where(r => r.NeededByEnd >= now)
                        .Select(r => (DateTime?)r.NeededByEnd)
                        .Min()
                    : null;
                return dto;
            })
            .ToList();

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Durable goods loaded.",
            Items = items,
            HasMore = page.HasMore || page.Items.Count > items.Count
        };
    }
}
