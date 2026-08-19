using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Queries.GetLibraryRequestDetail;

public record GetLibraryRequestDetailQuery(int RequestId) : IRequest<LibraryRequestDetailResponse>;

public class GetLibraryRequestDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    LibraryRequestPriorityService priorityService,
    IUnitOfWork unitOfWork) : IRequestHandler<GetLibraryRequestDetailQuery, LibraryRequestDetailResponse>
{
    public async Task<LibraryRequestDetailResponse> Handle(
        GetLibraryRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestDetailResponse { Success = false, Message = "You are not in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        await libraryRepository.ExpireStaleOpenRequestsForCrewAsync(membership.CrewId, utcNow, cancellationToken);

        var tracked = await libraryRepository.GetTrackedRequestByIdAsync(request.RequestId, cancellationToken);
        if (tracked is not null)
        {
            LibraryRequestExpiryService.TryExpireDeniedRequest(tracked, utcNow);
            LibraryRequestExpiryService.TryExpireOpenRequest(tracked, utcNow);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var libraryRequest = await libraryRepository.GetRequestByIdForCrewAsync(
            request.RequestId,
            membership.CrewId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestDetailResponse { Success = false, Message = "Request not found." };
        }

        if (!LibraryRequestAccess.CanView(libraryRequest, userId))
        {
            return new LibraryRequestDetailResponse { Success = false, Message = "You do not have access to this request." };
        }

        var openCount = libraryRequest.Status == LibraryRequestStatus.Open
            ? await libraryRepository.CountOpenRequestsForUnitAsync(libraryRequest.UnitId, cancellationToken)
            : 0;

        var detail = LibraryMapper.MapRequestDetail(libraryRequest, userId, openCount);
        if (detail.IsPossessorView && libraryRequest.Status == LibraryRequestStatus.Open)
        {
            var unitOpenRequests = await libraryRepository.GetOpenRequestsForUnitAsync(
                libraryRequest.UnitId,
                membership.CrewId,
                cancellationToken);
            await priorityService.ApplyPossessorPriorityToDetailAsync(
                detail,
                libraryRequest,
                unitOpenRequests,
                membership.CrewId,
                cancellationToken);
        }

        return new LibraryRequestDetailResponse
        {
            Success = true,
            Message = "Request loaded.",
            Item = detail
        };
    }
}
