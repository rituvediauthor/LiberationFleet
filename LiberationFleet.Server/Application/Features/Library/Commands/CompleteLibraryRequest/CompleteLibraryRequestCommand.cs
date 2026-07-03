using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.CompleteLibraryRequest;

public record CompleteLibraryRequestCommand(int RequestId) : IRequest<LibraryCompleteRequestResponse>;

public class CompleteLibraryRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    IGiftRepository giftRepository,
    LibraryContributionGiftService contributionGiftService,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteLibraryRequestCommand, LibraryCompleteRequestResponse>
{
    public async Task<LibraryCompleteRequestResponse> Handle(
        CompleteLibraryRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "You are not in a crew." };
        }

        var libraryRequest = await libraryRepository.GetTrackedRequestWithUnitForCompleteAsync(
            request.RequestId,
            userId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Request not found." };
        }

        if (libraryRequest.Status != LibraryRequestStatus.Open)
        {
            return new LibraryCompleteRequestResponse { Success = false, Message = "Only open requests can be completed." };
        }

        var utcNow = DateTime.UtcNow;
        var messageIds = await libraryRepository.DeleteMessagesForRequestAsync(libraryRequest.Id, cancellationToken);
        if (messageIds.Count > 0)
        {
            await cryptoRepository.DeleteEnvelopesAsync(
                EncryptedContentType.LibraryRequestMessage,
                messageIds.Select(id => id.ToString()).ToList(),
                cancellationToken);
        }

        var offering = libraryRequest.Unit.Offering;
        CreatorContributionGiftDetails? contributionGift = null;
        CreatorContributionGiftDetails? completerGift = null;
        CreatorContributionGiftDetails? receptionGift = null;
        if (LibraryOfferingRules.IsStockBased(offering))
        {
            if (!LibraryOfferingRules.HasSufficientStock(offering, libraryRequest.Quantity))
            {
                return new LibraryCompleteRequestResponse { Success = false, Message = "Not enough stock available." };
            }

            LibraryOfferingRules.ReduceStock(offering, libraryRequest.Quantity);
            offering.UpdatedAt = utcNow;
            if (!offering.QuantityNotApplicable && offering.RemainingStock <= 0)
            {
                libraryRequest.Unit.Status = LibraryUnitStatus.Broken;
            }

            contributionGift = await contributionGiftService.TryAwardCreatorForStockUseAsync(
                membership.CrewId,
                offering,
                libraryRequest.Quantity,
                libraryRequest.RequesterUserId,
                libraryRequest.RequesterUser.Username,
                cancellationToken);

            receptionGift = await contributionGiftService.TryAwardRecipientReceptionForStockUseAsync(
                membership.CrewId,
                offering,
                libraryRequest.Quantity,
                libraryRequest.RequesterUserId,
                libraryRequest.RequesterUser.Username,
                cancellationToken);
        }
        else
        {
            var completerUsername = libraryRequest.Unit.CurrentPossessorUser?.Username ?? "Crewmate";
            libraryRequest.Unit.CurrentPossessorUserId = libraryRequest.RequesterUserId;
            contributionGift = await contributionGiftService.TryAwardCreatorForFirstDurableTransferAsync(
                membership.CrewId,
                libraryRequest.Unit,
                offering,
                libraryRequest.RequesterUserId,
                libraryRequest.RequesterUser.Username,
                libraryRequest.Quantity,
                cancellationToken);

            completerGift = await contributionGiftService.TryAwardCompleterForDurableHandoffAsync(
                membership.CrewId,
                offering,
                libraryRequest.Quantity,
                userId,
                completerUsername,
                libraryRequest.RequesterUserId,
                libraryRequest.RequesterUser.Username,
                cancellationToken);
        }

        libraryRequest.Status = LibraryRequestStatus.Fulfilled;
        libraryRequest.UpdatedAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (receptionGift is not null)
        {
            var receptionRecord = await giftRepository.GetByIdWithUsersAsync(receptionGift.GiftId, cancellationToken);
            if (receptionRecord is not null)
            {
                await mutualAidService.ApplyGiftReceptionAsync(receptionRecord, cancellationToken);
            }
        }

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = libraryRequest.RequesterUserId,
            CrewId = membership.CrewId,
            Kind = NotificationKind.LibraryRequestCompleted,
            Title = "Library request completed",
            Body = $"Your request for {libraryRequest.Unit.Offering.Title} was fulfilled.",
            ActionUrl = $"/app/crew/library-of-things/requests/mine",
            RelatedEntityId = libraryRequest.Id
        }, cancellationToken);

        return new LibraryCompleteRequestResponse
        {
            Success = true,
            Message = "Request completed.",
            RequestId = libraryRequest.Id,
            GiftId = contributionGift?.GiftId ?? completerGift?.GiftId ?? receptionGift?.GiftId,
            ContributionGift = LibraryMapper.MapContributionGift(contributionGift),
            CompleterGift = LibraryMapper.MapContributionGift(completerGift),
            ReceptionGift = LibraryMapper.MapContributionGift(receptionGift)
        };
    }
}
