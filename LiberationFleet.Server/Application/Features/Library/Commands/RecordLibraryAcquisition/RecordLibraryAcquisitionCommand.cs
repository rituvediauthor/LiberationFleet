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

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Encrypted note is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "You are not in a crew." };
        }

        var unit = await libraryRepository.GetUnitByIdForCrewAsync(request.UnitId, membership.CrewId, cancellationToken);
        if (unit is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Item not found." };
        }

        var offering = unit.Offering;
        if (!LibraryOfferingRules.IsOnDemand(offering))
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "This offering is not available for on-demand acquisition." };
        }

        var quantity = offering.QuantityNotApplicable ? 1 : request.Quantity;
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
            HasEncryptedContent = true,
            Status = LibraryRequestStatus.Fulfilled,
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await libraryRepository.AddRequestAsync(libraryRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryRequest,
            ResourceId = libraryRequest.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        trackedUnit.Offering.UpdatedAt = utcNow;
        LibraryOfferingRules.ReduceStock(trackedUnit.Offering, quantity);
        if (!trackedUnit.Offering.QuantityNotApplicable && trackedUnit.Offering.RemainingStock <= 0)
        {
            trackedUnit.Status = LibraryUnitStatus.Broken;
        }

        var acquirer = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var acquirerUsername = acquirer?.Username ?? "Crewmate";
        var contributionGift = await contributionGiftService.TryAwardCreatorForStockUseAsync(
            membership.CrewId,
            trackedUnit.Offering,
            quantity,
            userId,
            acquirerUsername,
            cancellationToken);

        var receptionGift = await contributionGiftService.TryAwardRecipientReceptionForStockUseAsync(
            membership.CrewId,
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
        }

        return new LibraryCompleteRequestResponse
        {
            Success = true,
            Message = "Acquisition recorded.",
            RequestId = libraryRequest.Id,
            GiftId = contributionGift?.GiftId ?? receptionGift?.GiftId,
            ContributionGift = LibraryMapper.MapContributionGift(contributionGift),
            ReceptionGift = LibraryMapper.MapContributionGift(receptionGift)
        };
    }
}
