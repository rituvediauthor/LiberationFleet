using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.DismissDeniedLibraryRequests;

public record DismissDeniedLibraryRequestsCommand(int? RequestId = null) : IRequest<LibraryRequestOperationResponse>;

public class DismissDeniedLibraryRequestsCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    LibraryRequestCleanupHelper requestCleanupHelper,
    IUnitOfWork unitOfWork) : IRequestHandler<DismissDeniedLibraryRequestsCommand, LibraryRequestOperationResponse>
{
    public async Task<LibraryRequestOperationResponse> Handle(
        DismissDeniedLibraryRequestsCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        if (await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken) is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var denied = await libraryRepository.GetTrackedDeniedRequestsForRequesterAsync(userId, cancellationToken);
        if (request.RequestId.HasValue)
        {
            denied = denied.Where(r => r.Id == request.RequestId.Value).ToList();
            if (denied.Count == 0)
            {
                return new LibraryRequestOperationResponse { Success = false, Message = "Denied request not found." };
            }
        }

        if (denied.Count == 0)
        {
            return new LibraryRequestOperationResponse { Success = true, Message = "No denied requests to dismiss." };
        }

        var utcNow = DateTime.UtcNow;
        foreach (var libraryRequest in denied)
        {
            await requestCleanupHelper.CancelRequestWithMessagesAsync(libraryRequest.Id, cancellationToken);
            libraryRequest.Status = LibraryRequestStatus.Cancelled;
            libraryRequest.UpdatedAt = utcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryRequestOperationResponse
        {
            Success = true,
            Message = denied.Count == 1 ? "Denied request dismissed." : $"{denied.Count} denied requests dismissed.",
            RequestId = request.RequestId ?? denied[0].Id
        };
    }
}
