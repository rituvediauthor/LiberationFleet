using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.UpdateLibraryOffering;

public record UpdateLibraryOfferingCommand(
    int OfferingId,
    bool? IsOutOfStock,
    LibraryOfferingVisibility? Visibility,
    string? ThumbnailResourceId,
    string? Nonce,
    string? Ciphertext,
    int? KeyVersion)
    : IRequest<LibraryOfferingOperationResponse>;

public class UpdateLibraryOfferingCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateLibraryOfferingCommand, LibraryOfferingOperationResponse>
{
    public async Task<LibraryOfferingOperationResponse> Handle(
        UpdateLibraryOfferingCommand request,
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

        if (!LibraryUnitAccess.CanEditOffering(offering, userId))
        {
            return new LibraryOfferingOperationResponse { Success = false, Message = "You cannot edit this offering." };
        }

        var changed = false;

        if (request.IsOutOfStock.HasValue)
        {
            if (!offering.QuantityNotApplicable)
            {
                return new LibraryOfferingOperationResponse
                {
                    Success = false,
                    Message = "Only variable-quantity offerings can be manually marked out of stock."
                };
            }

            offering.IsOutOfStock = request.IsOutOfStock.Value;
            changed = true;
        }

        if (request.Visibility.HasValue)
        {
            offering.Visibility = request.Visibility.Value;
            changed = true;
        }

        var replacingEncryptedContent = !string.IsNullOrWhiteSpace(request.Nonce)
            && !string.IsNullOrWhiteSpace(request.Ciphertext);
        if (replacingEncryptedContent)
        {
            if (offering.Kind != LibraryOfferingKind.Digital)
            {
                return new LibraryOfferingOperationResponse
                {
                    Success = false,
                    Message = "Only digital offerings can replace download files."
                };
            }

            var utcNow = DateTime.UtcNow;
            await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
            {
                ContentType = EncryptedContentType.LibraryItem,
                ResourceId = offering.Id.ToString(),
                CrewId = membership.CrewId,
                AuthorUserId = userId,
                KeyVersion = request.KeyVersion is > 0 ? request.KeyVersion.Value : 1,
                Nonce = request.Nonce!.Trim(),
                Ciphertext = request.Ciphertext!.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }, cancellationToken);

            if (!string.IsNullOrWhiteSpace(request.ThumbnailResourceId))
            {
                offering.ThumbnailResourceId = request.ThumbnailResourceId.Trim();
            }

            changed = true;
        }

        if (changed)
        {
            offering.UpdatedAt = DateTime.UtcNow;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new LibraryOfferingOperationResponse
        {
            Success = true,
            Message = "Offering updated.",
            OfferingId = offering.Id
        };
    }
}
