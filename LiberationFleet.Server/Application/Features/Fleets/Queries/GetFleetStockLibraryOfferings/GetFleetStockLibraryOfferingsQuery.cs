using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetStockLibraryOfferings;

public record GetFleetStockLibraryOfferingsQuery(
    LibraryOfferingKind Kind,
    string? Search,
    IReadOnlyList<int> CategoryIds,
    int Limit = 30,
    int Offset = 0) : IRequest<LibraryUnitListResponse>;

public class GetFleetStockLibraryOfferingsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository,
    LibraryPriorityTierService priorityTierService) : IRequestHandler<GetFleetStockLibraryOfferingsQuery, LibraryUnitListResponse>
{
    public async Task<LibraryUnitListResponse> Handle(
        GetFleetStockLibraryOfferingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryUnitListResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.Kind is not (LibraryOfferingKind.Consumable or LibraryOfferingKind.Service or LibraryOfferingKind.Digital))
        {
            return new LibraryUnitListResponse
            {
                Success = false,
                Message = "Kind must be Consumable, Service, or Digital."
            };
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

        var summary = await priorityTierService.GetSummaryForUserAsync(
            currentUser.UserId.Value,
            membership.CrewId,
            cancellationToken);
        var viewerTier = summary.ViewerTier;

        var fetchLimit = Math.Clamp(request.Limit, 1, 100);
        var page = await libraryRepository.GetStockUnitsForCrewIdsAsync(
            crewIds,
            membership.CrewId,
            request.Kind,
            request.Search,
            request.CategoryIds,
            Math.Min(100, fetchLimit * 3),
            Math.Max(request.Offset, 0),
            cancellationToken);

        var items = page.Items
            .Where(unit => LibraryOfferingRules.IsVisibleToViewerTier(unit.Offering, viewerTier))
            .Where(unit =>
                unit.Offering.QuantityNotApplicable
                || !LibraryOfferingRules.UsesPerTierStock(unit.Offering)
                || LibraryOfferingRules.HasAvailableStockForTier(unit.Offering, viewerTier))
            .Take(fetchLimit)
            .Select(unit => LibraryMapper.MapUnitListItem(unit, viewerTier))
            .ToList();

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Fleet offerings loaded.",
            Items = items,
            HasMore = page.HasMore || page.Items.Count > items.Count
        };
    }
}
