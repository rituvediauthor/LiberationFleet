using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.RecordLibraryMaintenance;

public record RecordLibraryMaintenanceCommand(
    int UnitId,
    decimal Cost,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryMaintenanceOperationResponse>;

public class RecordLibraryMaintenanceCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    LibraryContributionGiftService contributionGiftService,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordLibraryMaintenanceCommand, LibraryMaintenanceOperationResponse>
{
    public async Task<LibraryMaintenanceOperationResponse> Handle(
        RecordLibraryMaintenanceCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryMaintenanceOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.Cost <= 0)
        {
            return new LibraryMaintenanceOperationResponse { Success = false, Message = "Maintenance cost must be greater than zero." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new LibraryMaintenanceOperationResponse { Success = false, Message = "Encrypted maintenance notes are required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryMaintenanceOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var trackedUnit = await libraryRepository.GetTrackedUnitByIdAsync(request.UnitId, cancellationToken);
        if (trackedUnit is null || trackedUnit.Offering.CrewId != membership.CrewId)
        {
            return new LibraryMaintenanceOperationResponse { Success = false, Message = "Item not found." };
        }

        if (!LibraryUnitAccess.CanRecordMaintenance(trackedUnit, userId))
        {
            return new LibraryMaintenanceOperationResponse { Success = false, Message = "You cannot record maintenance for this item." };
        }

        var utcNow = DateTime.UtcNow;
        var record = new LibraryMaintenanceRecord
        {
            UnitId = trackedUnit.Id,
            ContributorUserId = userId,
            Cost = request.Cost,
            HasEncryptedContent = true,
            CreatedAt = utcNow
        };

        await libraryRepository.AddMaintenanceRecordAsync(record, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryMaintenanceRecord,
            ResourceId = record.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        var gift = await contributionGiftService.CreateContributionGiftAsync(
            membership.CrewId,
            userId,
            request.Cost,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryMaintenanceOperationResponse
        {
            Success = true,
            Message = "Maintenance recorded.",
            MaintenanceId = record.Id,
            GiftId = gift.Id
        };
    }
}
