using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.UndenyLibraryRequest;

public record UndenyLibraryRequestCommand(int RequestId) : IRequest<LibraryRequestOperationResponse>;

public class UndenyLibraryRequestCommandHandler(
    ICurrentUserService currentUser,
    ILibraryRepository libraryRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UndenyLibraryRequestCommand, LibraryRequestOperationResponse>
{
    public async Task<LibraryRequestOperationResponse> Handle(
        UndenyLibraryRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var libraryRequest = await libraryRepository.GetTrackedRequestByIdForPossessorAsync(
            request.RequestId,
            userId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Request not found." };
        }

        if (libraryRequest.Status != LibraryRequestStatus.Denied)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Only denied requests can be restored." };
        }

        libraryRequest.Status = LibraryRequestStatus.Open;
        libraryRequest.DeniedAt = null;
        libraryRequest.UpdatedAt = DateTime.UtcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryRequestOperationResponse
        {
            Success = true,
            Message = "Request restored to the queue.",
            RequestId = libraryRequest.Id
        };
    }
}
