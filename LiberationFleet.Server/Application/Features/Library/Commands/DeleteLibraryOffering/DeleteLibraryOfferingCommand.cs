using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.DeleteLibraryOffering;

public record DeleteLibraryOfferingCommand(int OfferingId) : IRequest<LibraryOfferingOperationResponse>;

public class DeleteLibraryOfferingCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    LibraryRequestCleanupHelper requestCleanupHelper,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteLibraryOfferingCommand, LibraryOfferingOperationResponse>
{
    public async Task<LibraryOfferingOperationResponse> Handle(
        DeleteLibraryOfferingCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var offering = await libraryRepository.GetTrackedOfferingByIdAsync(request.OfferingId, cancellationToken);
        if (offering is null || offering.CrewId != membership.CrewId)
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "Offering not found." };
        }

        if (!LibraryUnitAccess.CanDeleteOffering(offering, userId))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "You cannot delete this offering." };
        }

        foreach (var unit in offering.Units)
        {
            await requestCleanupHelper.CancelActiveRequestsForUnitAsync(unit.Id, cancellationToken);
        }

        if (offering.HasEncryptedContent)
        {
            await cryptoRepository.DeleteEnvelopesAsync(
                EncryptedContentType.LibraryItem,
                [offering.Id.ToString()],
                cancellationToken);
        }

        if (!string.IsNullOrWhiteSpace(offering.ThumbnailResourceId))
        {
            await cryptoRepository.DeleteEnvelopesAsync(
                EncryptedContentType.ImageAsset,
                [offering.ThumbnailResourceId],
                cancellationToken);
        }

        var utcNow = DateTime.UtcNow;
        offering.IsDeleted = true;
        offering.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryOfferingOperationResponse
        {
            Success = true,
            Message = "Offering deleted.",
            OfferingId = offering.Id
        };
    }
}
