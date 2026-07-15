using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetLibraryUnitDetail;

public record GetFleetLibraryUnitDetailQuery(int UnitId) : IRequest<LibraryUnitDetailResponse>;

public class GetFleetLibraryUnitDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetFleetLibraryUnitDetailQuery, LibraryUnitDetailResponse>
{
    public async Task<LibraryUnitDetailResponse> Handle(
        GetFleetLibraryUnitDetailQuery request,
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

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new LibraryUnitDetailResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        if (!fleet.LibraryOfThingsEnabled)
        {
            return new LibraryUnitDetailResponse { Success = false, Message = "Fleet library is disabled." };
        }

        var crewIds = (await fleetRepository.GetFleetCrewsAsync(fleet.Id, cancellationToken))
            .Select(fc => fc.CrewId)
            .ToList();

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

        var viewer = LibraryRequestValidation.BuildViewerContext(
            unit,
            isHolder,
            hasOpenRequest,
            activeRequest,
            userId);

        return new LibraryUnitDetailResponse
        {
            Success = true,
            Message = "Item loaded.",
            Item = LibraryMapper.MapUnitDetail(unit, viewer)
        };
    }
}
