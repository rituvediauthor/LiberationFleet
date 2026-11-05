using LiberationFleet.Server.Application.Common;
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
    LibraryPriorityTierService priorityTierService,
    IUserBlockRepository blockRepository) : IRequestHandler<GetStockLibraryOfferingsQuery, LibraryUnitListResponse>
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
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(viewerUserId, cancellationToken);
        var tierByOfferingCrewId = new Dictionary<int, int>();

        var fetchLimit = Math.Clamp(request.Limit, 1, 100);
        var fetchOffset = Math.Max(request.Offset, 0);

        async Task<int> TierForAsync(int offeringCrewId, CancellationToken ct)
        {
            if (!tierByOfferingCrewId.TryGetValue(offeringCrewId, out var tier))
            {
                tier = await priorityTierService.GetViewerTierForOfferingAsync(
                    viewerUserId,
                    offeringCrewId,
                    ct);
                tierByOfferingCrewId[offeringCrewId] = tier;
            }

            return tier;
        }

        var (visibleUnits, hasMore) = await LibraryVisibleUnitPaging.TakeVisibleAsync(
            (limit, offset, ct) => libraryRepository.GetStockUnitsForCrewIdsAsync(
                crewIds,
                membership.CrewId,
                request.Kind,
                request.Search,
                request.CategoryIds,
                limit,
                offset,
                ct),
            async (unit, ct) =>
            {
                var viewerTier = await TierForAsync(unit.Offering.CrewId, ct);
                return !BlockVisibility.HidesAny(hiddenUserIds, unit.Offering.CreatorUserId, unit.CurrentPossessorUserId)
                    && LibraryOfferingRules.CanViewerAccessOffering(
                        unit.Offering,
                        viewerUserId,
                        viewerTier,
                        viewerCountry,
                        viewerZip,
                        unit.CurrentPossessorUserId,
                        membership.CrewId)
                    && (unit.Offering.QuantityNotApplicable
                        || !LibraryOfferingRules.UsesPerTierStock(unit.Offering)
                        || LibraryOfferingRules.HasAvailableStockForTier(unit.Offering, viewerTier)
                        || unit.Offering.CreatorUserId == viewerUserId);
            },
            fetchOffset,
            fetchLimit,
            cancellationToken);

        var items = visibleUnits
            .Select(unit => LibraryMapper.MapUnitListItem(unit, tierByOfferingCrewId[unit.Offering.CrewId]))
            .ToList();

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Offerings loaded.",
            Items = items,
            HasMore = hasMore
        };
    }
}
