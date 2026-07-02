using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetUnitActiveLibraryRequests;

public record GetUnitActiveLibraryRequestsQuery(int UnitId) : IRequest<LibraryRequestListResponse>;

public class GetUnitActiveLibraryRequestsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetUnitActiveLibraryRequestsQuery, LibraryRequestListResponse>
{
    public async Task<LibraryRequestListResponse> Handle(
        GetUnitActiveLibraryRequestsQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestListResponse { Success = false, Message = "You are not in a crew." };
        }

        var unit = await libraryRepository.GetUnitByIdForCrewAsync(request.UnitId, membership.CrewId, cancellationToken);
        if (unit is null)
        {
            return new LibraryRequestListResponse { Success = false, Message = "Item not found." };
        }

        if (unit.CurrentPossessorUserId != userId)
        {
            return new LibraryRequestListResponse { Success = false, Message = "You are not the holder of this item." };
        }

        var requests = await libraryRepository.GetOpenRequestsForUnitAsync(
            request.UnitId,
            membership.CrewId,
            cancellationToken);

        return new LibraryRequestListResponse
        {
            Success = true,
            Message = "Active requests loaded.",
            Items = requests.Select(LibraryMapper.MapRequestListItem).ToList()
        };
    }
}
