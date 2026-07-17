using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetMyLibraryOfferings;

public record GetMyLibraryOfferingsQuery(
    string? Search = null,
    IReadOnlyList<int>? CategoryIds = null,
    int Limit = 30,
    int Offset = 0) : IRequest<LibraryOfferingListResponse>;

public class GetMyLibraryOfferingsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetMyLibraryOfferingsQuery, LibraryOfferingListResponse>
{
    public async Task<LibraryOfferingListResponse> Handle(
        GetMyLibraryOfferingsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryOfferingListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryOfferingListResponse { Success = false, Message = "You are not in a crew." };
        }

        var page = await libraryRepository.GetOfferingsByCreatorAsync(
            membership.CrewId,
            userId,
            request.Search,
            request.CategoryIds ?? Array.Empty<int>(),
            Math.Clamp(request.Limit, 1, 100),
            Math.Max(request.Offset, 0),
            cancellationToken);

        return new LibraryOfferingListResponse
        {
            Success = true,
            Message = "Offerings loaded.",
            Items = page.Items.Select(LibraryMapper.MapOfferingListItem).ToList(),
            HasMore = page.HasMore
        };
    }
}
