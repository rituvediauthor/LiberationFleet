using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.ReportLibraryUnitFixed;

public record ReportLibraryUnitFixedCommand(int UnitId) : IRequest<LibraryUnitOperationResponse>;

public class ReportLibraryUnitFixedCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ReportLibraryUnitFixedCommand, LibraryUnitOperationResponse>
{
    public async Task<LibraryUnitOperationResponse> Handle(
        ReportLibraryUnitFixedCommand request,
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

        if (!LibraryUnitAccess.CanReportFixed(trackedUnit, userId))
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "You cannot report this item as fixed." };
        }

        trackedUnit.Status = LibraryUnitStatus.Available;
        trackedUnit.BrokenPendingConfirmation = false;
        trackedUnit.BrokenReportedAt = null;

        await cryptoRepository.DeleteEnvelopesAsync(
            EncryptedContentType.LibraryBrokenReport,
            [trackedUnit.Id.ToString()],
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            membership.CrewId,
            NotificationKind.LibraryUnitReportedFixed,
            "Library item reported fixed",
            $"{trackedUnit.Offering.Title} was reported fixed and is available again.",
            $"/app/crew/library-of-things/units/{trackedUnit.Id}",
            relatedEntityId: trackedUnit.Id,
            cancellationToken: cancellationToken);

        return new LibraryUnitOperationResponse
        {
            Success = true,
            Message = "Item reported as fixed.",
            UnitId = trackedUnit.Id
        };
    }
}
