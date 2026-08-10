using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.CancelLibraryRequest;

public record CancelLibraryRequestCommand(int RequestId) : IRequest<LibraryRequestOperationResponse>;

public class CancelLibraryRequestCommandHandler(
    ICurrentUserService currentUser,
    ILibraryRepository libraryRepository,
    LibraryRequestCleanupHelper requestCleanupHelper,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelLibraryRequestCommand, LibraryRequestOperationResponse>
{
    public async Task<LibraryRequestOperationResponse> Handle(
        CancelLibraryRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var libraryRequest = await libraryRepository.GetTrackedRequestByIdForRequesterAsync(
            request.RequestId,
            userId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Request not found." };
        }

        if (libraryRequest.Status != LibraryRequestStatus.Open
            && libraryRequest.Status != LibraryRequestStatus.Denied)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "This request cannot be deleted." };
        }

        var wasDenied = libraryRequest.Status == LibraryRequestStatus.Denied;
        await requestCleanupHelper.CancelRequestWithMessagesAsync(libraryRequest.Id, cancellationToken);
        libraryRequest.Status = LibraryRequestStatus.Cancelled;
        libraryRequest.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryRequestOperationResponse
        {
            Success = true,
            Message = wasDenied ? "Denied request dismissed." : "Request deleted.",
            RequestId = libraryRequest.Id
        };
    }
}
