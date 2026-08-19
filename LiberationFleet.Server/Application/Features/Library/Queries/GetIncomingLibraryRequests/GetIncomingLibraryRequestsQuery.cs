using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetIncomingLibraryRequests;

public record GetIncomingLibraryRequestsQuery() : IRequest<LibraryRequestListResponse>;

public class GetIncomingLibraryRequestsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    LibraryRequestPriorityService priorityService,
    IUnitOfWork unitOfWork) : IRequestHandler<GetIncomingLibraryRequestsQuery, LibraryRequestListResponse>
{
    public async Task<LibraryRequestListResponse> Handle(
        GetIncomingLibraryRequestsQuery request,
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

        var utcNow = DateTime.UtcNow;
        await libraryRepository.ExpireStaleOpenRequestsForCrewAsync(membership.CrewId, utcNow, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var requests = await libraryRepository.GetIncomingRequestsForPossessorAsync(
            membership.CrewId,
            userId,
            cancellationToken);

        var items = requests.Select(LibraryMapper.MapRequestListItem).ToList();
        await priorityService.ApplyPossessorPriorityAsync(items, requests, membership.CrewId, cancellationToken);

        return new LibraryRequestListResponse
        {
            Success = true,
            Message = "Incoming requests loaded.",
            Items = items
        };
    }
}
