using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
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
    ILibraryRepository libraryRepository,
    IViewerLocationAccessor viewerLocation,
    IUserBlockRepository blockRepository) : IRequestHandler<GetFleetDurableLibraryUnitsQuery, LibraryUnitListResponse>
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

        var viewerUserId = currentUser.UserId.Value;
        var viewerCountry = viewerLocation.CountryCode;
        var viewerZip = viewerLocation.ZipCode;
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(viewerUserId, cancellationToken);
        var fetchLimit = Math.Clamp(request.Limit, 1, 100);
        var fetchOffset = Math.Max(request.Offset, 0);

        var (visibleUnits, hasMore) = await LibraryVisibleUnitPaging.TakeVisibleAsync(
            (limit, offset, ct) => libraryRepository.GetDurableUnitsForCrewIdsAsync(
                crewIds,
                membership.CrewId,
                request.Search,
                request.CategoryIds,
                limit,
                offset,
                ct),
            unit => LibraryOfferingRules.CanViewerAccessOffering(
                    unit.Offering,
                    viewerUserId,
                    viewerTier: 1,
                    viewerCountry,
                    viewerZip,
                    unit.CurrentPossessorUserId,
                    membership.CrewId)
                && !BlockVisibility.HidesAny(hiddenUserIds, unit.Offering.CreatorUserId, unit.CurrentPossessorUserId),
            fetchOffset,
            fetchLimit,
            cancellationToken);

        var now = DateTime.UtcNow;
        var items = visibleUnits
            .Select(unit =>
            {
                var dto = LibraryMapper.MapUnitListItem(unit);
                var openWindows = unit.Requests
                    .Where(r => r.Status == LibraryRequestStatus.Open)
                    .ToList();
                var reservedNow = openWindows.Any(r =>
                    LibraryRequestLifecycle.IsReservedAt(r.NeededByStart, r.NeededByEnd, now));
                dto.AvailableNow = !reservedNow;
                dto.NextAvailableDate = reservedNow
                    ? openWindows
                        .Where(r => LibraryRequestLifecycle.IsReservedAt(r.NeededByStart, r.NeededByEnd, now))
                        .Select(r => (DateTime?)LibraryRequestLifecycle.GetExclusiveExpiryUtc(r.NeededByEnd))
                        .Min()
                    : null;
                return dto;
            })
            .ToList();

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Fleet durable goods loaded.",
            Items = items,
            HasMore = hasMore
        };
    }
}
