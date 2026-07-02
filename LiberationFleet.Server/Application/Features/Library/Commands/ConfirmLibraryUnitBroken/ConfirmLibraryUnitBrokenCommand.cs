using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.ConfirmLibraryUnitBroken;

public record ConfirmLibraryUnitBrokenCommand(int UnitId) : IRequest<LibraryUnitOperationResponse>;

public class ConfirmLibraryUnitBrokenCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmLibraryUnitBrokenCommand, LibraryUnitOperationResponse>
{
    public async Task<LibraryUnitOperationResponse> Handle(
        ConfirmLibraryUnitBrokenCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var trackedUnit = await libraryRepository.GetTrackedUnitByIdAsync(request.UnitId, cancellationToken);
        if (trackedUnit is null || trackedUnit.Offering.CrewId != membership.CrewId)
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "Item not found." };
        }

        if (!LibraryUnitAccess.CanConfirmBroken(trackedUnit, userId))
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "This item cannot be confirmed broken." };
        }

        trackedUnit.IsRetired = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = trackedUnit.CurrentPossessorUserId,
            CrewId = membership.CrewId,
            Kind = NotificationKind.LibraryUnitBrokenConfirmed,
            Title = "Broken item confirmed",
            Body = $"The crew confirmed {trackedUnit.Offering.Title} is broken and removed it from the library.",
            ActionUrl = $"/app/crew/library-of-things/mine",
            RelatedEntityId = trackedUnit.Id
        }, cancellationToken);

        return new LibraryUnitOperationResponse
        {
            Success = true,
            Message = "Broken status confirmed. Item removed from listings.",
            UnitId = trackedUnit.Id
        };
    }
}
