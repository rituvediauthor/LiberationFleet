using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryCategories;

public record GetLibraryCategoriesQuery(
    bool InUseOnly = false,
    string? Kind = null) : IRequest<LibraryCategoryListResponse>;

public class GetLibraryCategoriesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetLibraryCategoriesQuery, LibraryCategoryListResponse>
{
    public async Task<LibraryCategoryListResponse> Handle(
        GetLibraryCategoriesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryCategoryListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(
            currentUser.UserId.Value,
            cancellationToken);
        if (membership is null)
        {
            return new LibraryCategoryListResponse { Success = false, Message = "You are not in a crew." };
        }

        LibraryOfferingKind? kind = null;
        if (!string.IsNullOrWhiteSpace(request.Kind)
            && LibraryEnumParser.TryParseOfferingKind(request.Kind, out var parsedKind))
        {
            kind = parsedKind;
        }

        var categories = request.InUseOnly
            ? await libraryRepository.GetCategoriesInUseAsync(membership.CrewId, kind, cancellationToken)
            : await libraryRepository.GetCategoriesAsync(cancellationToken);

        return new LibraryCategoryListResponse
        {
            Success = true,
            Message = "Categories loaded.",
            Items = categories.Select(LibraryMapper.MapCategory).ToList()
        };
    }
}
