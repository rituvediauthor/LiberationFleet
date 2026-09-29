using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryOfferingForEdit;

public record GetLibraryOfferingForEditQuery(int OfferingId) : IRequest<LibraryOfferingDetailResponse>;

public class GetLibraryOfferingForEditQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetLibraryOfferingForEditQuery, LibraryOfferingDetailResponse>
{
    public async Task<LibraryOfferingDetailResponse> Handle(
        GetLibraryOfferingForEditQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryOfferingDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryOfferingDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var offering = await libraryRepository.GetTrackedOfferingByIdAsync(request.OfferingId, cancellationToken);
        if (offering is null
            || offering.IsDeleted
            || offering.CrewId != membership.CrewId
            || offering.CreatorUserId != userId)
        {
            return new LibraryOfferingDetailResponse { Success = false, Message = "Offering not found." };
        }

        return new LibraryOfferingDetailResponse
        {
            Success = true,
            Message = "Offering loaded.",
            Item = LibraryMapper.MapOfferingListItem(offering)
        };
    }
}
