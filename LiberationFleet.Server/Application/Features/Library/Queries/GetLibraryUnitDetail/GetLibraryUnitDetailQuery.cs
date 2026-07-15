using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryUnitDetail;

public record GetLibraryUnitDetailQuery(int UnitId) : IRequest<LibraryUnitDetailResponse>;

public class GetLibraryUnitDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetLibraryUnitDetailQuery, LibraryUnitDetailResponse>
{
    public async Task<LibraryUnitDetailResponse> Handle(
        GetLibraryUnitDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryUnitDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryUnitDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var crewIds = await LibraryScopeHelper.GetAccessibleCrewIdsAsync(
            membership.CrewId,
            fleetRepository,
            cancellationToken);

        var unit = await libraryRepository.GetUnitByIdForCrewIdsAsync(request.UnitId, crewIds, cancellationToken);
        if (unit is null)
        {
            return new LibraryUnitDetailResponse { Success = false, Message = "Item not found." };
        }

        var isHolder = unit.CurrentPossessorUserId == userId;
        var hasOpenRequest = await libraryRepository.HasOpenRequestForUnitByUserAsync(
            unit.Id,
            userId,
            cancellationToken);
        var activeRequest = await libraryRepository.GetActiveRequestByUnitAndRequesterAsync(
            unit.Id,
            userId,
            cancellationToken);

        var viewer = LibraryRequestValidation.BuildViewerContext(unit, isHolder, hasOpenRequest, activeRequest, userId);

        return new LibraryUnitDetailResponse
        {
            Success = true,
            Message = "Item loaded.",
            Item = LibraryMapper.MapUnitDetail(unit, viewer)
        };
    }
}
