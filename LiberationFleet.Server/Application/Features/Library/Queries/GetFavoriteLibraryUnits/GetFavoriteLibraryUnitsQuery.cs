using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetFavoriteLibraryUnits;

public record GetFavoriteLibraryUnitsQuery(
    string? Search,
    IReadOnlyList<int> CategoryIds,
    int Limit = 30,
    int Offset = 0) : IRequest<LibraryUnitListResponse>;

public class GetFavoriteLibraryUnitsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetFavoriteLibraryUnitsQuery, LibraryUnitListResponse>
{
    public async Task<LibraryUnitListResponse> Handle(
        GetFavoriteLibraryUnitsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryUnitListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryUnitListResponse { Success = false, Message = "You are not in a crew." };
        }

        var crewIds = await LibraryScopeHelper.GetAccessibleCrewIdsAsync(
            membership.CrewId,
            fleetRepository,
            cancellationToken);

        var page = await libraryRepository.GetFavoriteUnitsAsync(
            userId,
            crewIds,
            membership.CrewId,
            request.Search,
            request.CategoryIds,
            Math.Clamp(request.Limit, 1, 100),
            Math.Max(request.Offset, 0),
            cancellationToken);

        return new LibraryUnitListResponse
        {
            Success = true,
            Message = "Favorites loaded.",
            Items = page.Items.Select(unit => LibraryMapper.MapUnitListItem(unit)).ToList(),
            HasMore = page.HasMore
        };
    }
}

public record GetFavoriteLibraryUnitIdsQuery : IRequest<LibraryFavoriteIdsResponse>;

public class LibraryFavoriteIdsResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<int> UnitIds { get; set; } = Array.Empty<int>();
}

public class GetFavoriteLibraryUnitIdsQueryHandler(
    ICurrentUserService currentUser,
    ILibraryRepository libraryRepository) : IRequestHandler<GetFavoriteLibraryUnitIdsQuery, LibraryFavoriteIdsResponse>
{
    public async Task<LibraryFavoriteIdsResponse> Handle(
        GetFavoriteLibraryUnitIdsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryFavoriteIdsResponse { Success = false, Message = "Unauthorized." };
        }

        var ids = await libraryRepository.GetFavoriteUnitIdsAsync(currentUser.UserId.Value, cancellationToken);
        return new LibraryFavoriteIdsResponse
        {
            Success = true,
            Message = "Favorites loaded.",
            UnitIds = ids
        };
    }
}
