using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetMyLibraryRequests;

public record GetMyLibraryRequestsQuery() : IRequest<LibraryRequestListResponse>;

public class GetMyLibraryRequestsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository) : IRequestHandler<GetMyLibraryRequestsQuery, LibraryRequestListResponse>
{
    public async Task<LibraryRequestListResponse> Handle(
        GetMyLibraryRequestsQuery request,
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

        var requests = await libraryRepository.GetRequestsByRequesterAsync(
            membership.CrewId,
            userId,
            cancellationToken);

        return new LibraryRequestListResponse
        {
            Success = true,
            Message = "Requests loaded.",
            Items = requests.Select(LibraryMapper.MapRequestListItem).ToList()
        };
    }
}
