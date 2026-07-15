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
    ILibraryRepository libraryRepository) : IRequestHandler<GetStockLibraryOfferingsQuery, LibraryUnitListResponse>
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

        var page = await libraryRepository.GetStockUnitsForCrewIdsAsync(
            crewIds,
            request.Kind,
            request.Search,
            request.CategoryIds,
            Math.Clamp(request.Limit, 1, 100),
            Math.Max(request.Offset, 0),
            cancellationToken);

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Offerings loaded.",
            Items = page.Items.Select(LibraryMapper.MapUnitListItem).ToList(),
            HasMore = page.HasMore
        };
    }
}
