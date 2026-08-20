using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.RecordLibraryAcquisition;

public record RecordLibraryAcquisitionCommand(
    int UnitId,
    int Quantity,
    string PurposePreview,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryCompleteRequestResponse>;

public class RecordLibraryAcquisitionCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    IUserRepository userRepository,
    IGiftRepository giftRepository,
    LibraryContributionGiftService contributionGiftService,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordLibraryAcquisitionCommand, LibraryCompleteRequestResponse>
{
    public async Task<LibraryCompleteRequestResponse> Handle(
        RecordLibraryAcquisitionCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Unauthorized." };
        }

        // A note is optional for acquisitions (e.g. "I grabbed 6 eggs" needs only a quantity).
        var hasEncryptedNote = !string.IsNullOrWhiteSpace(request.Nonce)
            && !string.IsNullOrWhiteSpace(request.Ciphertext);

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "You are not in a crew." };
        }

        var crewIds = await LibraryScopeHelper.GetAccessibleCrewIdsAsync(
            membership.CrewId,
            fleetRepository,
            cancellationToken);
        var unit = await libraryRepository.GetUnitByIdForCrewIdsAsync(
            request.UnitId,
            crewIds,
            membership.CrewId,
            cancellationToken);
        if (unit is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Item not found." };
        }

        var offering = unit.Offering;
        if (!LibraryOfferingRules.IsOnDemand(offering))
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "This offering is not available for on-demand acquisition." };
        }

        // Quantity is meaningful even for "N/A" stock (e.g. 6 eggs); only durables force 1,
        // and durables never reach this on-demand path. Digital downloads are always qty 1.
        var quantity = LibraryOfferingRules.IsDigital(offering) ? 1 : request.Quantity;
        if (quantity < 1)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Quantity must be at least 1." };
        }

        if (!LibraryOfferingRules.HasSufficientStock(offering, quantity))
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Not enough stock available." };
        }

        var trackedUnit = await libraryRepository.GetTrackedUnitByIdAsync(unit.Id, cancellationToken);
        if (trackedUnit is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Item not found." };
        }

        var offeringCrewId = trackedUnit.Offering.CrewId;
        var utcNow = DateTime.UtcNow;
        var today = DateTime.UtcNow.Date;
        var libraryRequest = new LibraryRequest
        {
            UnitId = trackedUnit.Id,
            RequesterUserId = userId,
            Quantity = quantity,
            NeededByStart = today,
            NeededByEnd = today,
            PurposePreview = LibraryRequestValidation.NormalizePurposePreview(request.PurposePreview),
            HasEncryptedContent = hasEncryptedNote,
            Status = LibraryRequestStatus.Fulfilled,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await libraryRepository.AddRequestAsync(libraryRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (hasEncryptedNote)
        {
            await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
            {
                ContentType = EncryptedContentType.LibraryRequest,
                ResourceId = libraryRequest.Id.ToString(),
                CrewId = offeringCrewId,
                AuthorUserId = userId,
                KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
                Nonce = request.Nonce.Trim(),
                Ciphertext = request.Ciphertext.Trim(),
                CreatedAt = utcNow,
                UpdatedAt = utcNow
            }, cancellationToken);
        }

        trackedUnit.Offering.UpdatedAt = utcNow;
        LibraryOfferingRules.ReduceStock(trackedUnit.Offering, quantity);
        if (!trackedUnit.Offering.QuantityNotApplicable && trackedUnit.Offering.RemainingStock <= 0)
        {
            trackedUnit.Status = LibraryUnitStatus.Broken;
        }

        var acquirer = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var acquirerUsername = acquirer?.Username ?? "Crewmate";
        // Single gift-log entry: creator contribution (financial membership) + recipient reception.
        var receptionGift = await contributionGiftService.TryAwardCreatorForStockUseAsync(
            offeringCrewId,
            trackedUnit.Offering,
            quantity,
            userId,
            acquirerUsername,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (receptionGift is not null)
        {
            var receptionRecord = await giftRepository.GetByIdWithUsersAsync(receptionGift.GiftId, cancellationToken);
            if (receptionRecord is not null)
            {
                await mutualAidService.ApplyGiftReceptionAsync(receptionRecord, cancellationToken);
            }

            await mutualAidService.OnCrewContributionsChangedAsync(offeringCrewId, cancellationToken);
        }

        return new LibraryCompleteRequestResponse
        {
            Success = true,
            Message = "Acquisition recorded.",
            RequestId = libraryRequest.Id,
            GiftId = receptionGift?.GiftId,
            ReceptionGift = LibraryMapper.MapContributionGift(receptionGift)
        };
    }
}
