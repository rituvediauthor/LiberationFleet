using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.DenyLibraryRequest;

public record DenyLibraryRequestCommand(int RequestId) : IRequest<LibraryRequestOperationResponse>;

public class DenyLibraryRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<DenyLibraryRequestCommand, LibraryRequestOperationResponse>
{
    public async Task<LibraryRequestOperationResponse> Handle(
        DenyLibraryRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var libraryRequest = await libraryRepository.GetTrackedRequestByIdForPossessorAsync(
            request.RequestId,
            userId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Request not found." };
        }

        if (libraryRequest.Status != LibraryRequestStatus.Open)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Only open requests can be denied." };
        }

        var utcNow = DateTime.UtcNow;
        libraryRequest.Status = LibraryRequestStatus.Denied;
        libraryRequest.DeniedAt = utcNow;
        libraryRequest.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = libraryRequest.RequesterUserId,
            CrewId = membership.CrewId,
            Kind = NotificationKind.LibraryRequestDenied,
            Title = "Library request denied",
            Body = "A holder denied your library request.",
            ActionUrl = $"/app/crew/library-of-things/requests/{libraryRequest.Id}",
            RelatedEntityId = libraryRequest.Id
        }, cancellationToken);

        return new LibraryRequestOperationResponse
        {
            Success = true,
            Message = "Request denied.",
            RequestId = libraryRequest.Id
        };
    }
}
