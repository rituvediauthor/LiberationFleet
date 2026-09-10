using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetStockLibraryOfferings;

public record GetStockLibraryOfferingsQuery(
    LibraryOfferingKind Kind,
    string? Search,
    IReadOnlyList<int> CategoryIds,
    int Limit = 30,
    int Offset = 0) : IRequest<LibraryUnitListResponse>;

public class GetStockLibraryOfferingsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository,
    IViewerLocationAccessor viewerLocation,
    LibraryPriorityTierService priorityTierService) : IRequestHandler<GetStockLibraryOfferingsQuery, LibraryUnitListResponse>
{
    public async Task<LibraryUnitListResponse> Handle(
        GetStockLibraryOfferingsQuery request,
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
        var viewerUserId = currentUser.UserId.Value;
        var tierByOfferingCrewId = new Dictionary<int, int>();

        // Over-fetch then filter by tier/zip so paging still returns a full page when possible.
        var fetchLimit = Math.Clamp(request.Limit, 1, 100);
        var fetchOffset = Math.Max(request.Offset, 0);
        var page = await libraryRepository.GetStockUnitsForCrewIdsAsync(
            crewIds,
            membership.CrewId,
            request.Kind,
            request.Search,
            request.CategoryIds,
            Math.Min(100, fetchLimit * 3),
            fetchOffset,
            cancellationToken);

        foreach (var offeringCrewId in page.Items.Select(u => u.Offering.CrewId).Distinct())
        {
            tierByOfferingCrewId[offeringCrewId] = await priorityTierService.GetViewerTierForOfferingAsync(
                viewerUserId,
                offeringCrewId,
                cancellationToken);
        }

        var items = page.Items
            .Where(unit =>
            {
                var viewerTier = tierByOfferingCrewId[unit.Offering.CrewId];
                return LibraryOfferingRules.IsVisibleToViewerTier(unit.Offering, viewerTier)
                    && LibraryOfferingRules.IsVisibleToViewerZip(unit.Offering, viewerCountry, viewerZip)
                    && (unit.Offering.QuantityNotApplicable
                        || !LibraryOfferingRules.UsesPerTierStock(unit.Offering)
                        || LibraryOfferingRules.HasAvailableStockForTier(unit.Offering, viewerTier));
            })
            .Take(fetchLimit)
            .Select(unit => LibraryMapper.MapUnitListItem(unit, tierByOfferingCrewId[unit.Offering.CrewId]))
            .ToList();

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Offerings loaded.",
            Items = items,
            HasMore = page.HasMore || page.Items.Count > items.Count
        };
    }
}
