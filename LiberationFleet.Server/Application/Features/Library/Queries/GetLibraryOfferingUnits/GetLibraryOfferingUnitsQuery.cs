using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryOfferingUnits;

public record GetLibraryOfferingUnitsQuery(int OfferingId) : IRequest<LibraryUnitListResponse>;

public class GetLibraryOfferingUnitsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetLibraryOfferingUnitsQuery, LibraryUnitListResponse>
{
    public async Task<LibraryUnitListResponse> Handle(
        GetLibraryOfferingUnitsQuery request,
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

        var units = await libraryRepository.GetUnitsForOfferingAsync(
            request.OfferingId,
            crewIds,
            membership.CrewId,
            cancellationToken);

        if (units.Count == 0)
        {
            return new LibraryUnitListResponse { Success = false, Message = "Item not found." };
        }

        var now = DateTime.UtcNow;
        var unitCount = units.Count;

        var items = units
            .Select(unit =>
            {
                var dto = LibraryMapper.MapUnitListItem(unit);
                dto.OfferingUnitCount = unitCount;

                var openWindows = unit.Requests
                    .Where(r => r.Status == LibraryRequestStatus.Open)
                    .ToList();

                var reservedNow = openWindows
                    .Any(r => r.NeededByStart <= now && r.NeededByEnd >= now);

                dto.AvailableNow = !reservedNow;
                dto.NextAvailableDate = reservedNow
                    ? openWindows
                        .Where(r => r.NeededByEnd >= now)
                        .Select(r => (DateTime?)r.NeededByEnd)
                        .Min()
                    : null;

                return dto;
            })
            // Available first, then soonest to free up; least-soon (and unknown) last.
            .OrderByDescending(dto => dto.AvailableNow)
            .ThenBy(dto => dto.NextAvailableDate ?? DateTime.MaxValue)
            .ThenBy(dto => dto.UnitId)
            .ToList();

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Units loaded.",
            Items = items,
            HasMore = false
        };
    }
}
