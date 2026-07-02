using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.ReportLibraryUnitBroken;

public record ReportLibraryUnitBrokenCommand(
    int UnitId,
    string ExplanationPreview,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryUnitOperationResponse>;

public class ReportLibraryUnitBrokenCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    LibraryRequestCleanupHelper requestCleanupHelper,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<ReportLibraryUnitBrokenCommand, LibraryUnitOperationResponse>
{
    public async Task<LibraryUnitOperationResponse> Handle(
        ReportLibraryUnitBrokenCommand request,
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

        if (!LibraryUnitAccess.CanReportBroken(trackedUnit, userId))
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "You cannot report this item as broken." };
        }

        await requestCleanupHelper.CancelActiveRequestsForUnitAsync(trackedUnit.Id, cancellationToken);

        var utcNow = DateTime.UtcNow;
        trackedUnit.Status = LibraryUnitStatus.Broken;
        trackedUnit.BrokenPendingConfirmation = true;
        trackedUnit.BrokenReportedAt = utcNow;

        if (!string.IsNullOrWhiteSpace(request.Nonce) && !string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
            {
                ContentType = EncryptedContentType.LibraryBrokenReport,
                ResourceId = trackedUnit.Id.ToString(),
                CrewId = membership.CrewId,
                AuthorUserId = userId,
                KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
                Nonce = request.Nonce.Trim(),
                Ciphertext = request.Ciphertext.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            membership.CrewId,
            NotificationKind.LibraryUnitBrokenReported,
            "Library item reported broken",
            $"{trackedUnit.Offering.Title} was reported broken and needs confirmation.",
            $"/app/crew/library-of-things/units/{trackedUnit.Id}",
            relatedEntityId: trackedUnit.Id,
            cancellationToken: cancellationToken);

        return new LibraryUnitOperationResponse
        {
            Success = true,
            Message = "Item reported as broken.",
            UnitId = trackedUnit.Id
        };
    }
}
