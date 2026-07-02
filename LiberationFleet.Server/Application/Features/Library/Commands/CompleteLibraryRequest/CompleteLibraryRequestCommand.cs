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
    LibraryContributionGiftService contributionGiftService,
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
        }
        else
        {
            libraryRequest.Unit.CurrentPossessorUserId = libraryRequest.RequesterUserId;
        }

        libraryRequest.Status = LibraryRequestStatus.Fulfilled;
        libraryRequest.UpdatedAt = utcNow;

        var giftAmount = LibraryRequestAccess.CalculateCompletionGiftAmount(libraryRequest);
        var gift = await contributionGiftService.CreateContributionGiftAsync(
            membership.CrewId,
            userId,
            giftAmount,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

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
            GiftId = gift.Id
        };
    }
}
