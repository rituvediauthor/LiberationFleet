using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.CreateLibraryRequest;

public record CreateLibraryRequestCommand(
    int UnitId,
    int Quantity,
    string PurposePreview,
    DateTime NeededByStart,
    DateTime NeededByEnd,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryRequestOperationResponse>;

public class CreateLibraryRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateLibraryRequestCommand, LibraryRequestOperationResponse>
{
    public async Task<LibraryRequestOperationResponse> Handle(
        CreateLibraryRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Encrypted purpose is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "You are not in a crew." };
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
            return new LibraryRequestOperationResponse { Success = false, Message = "Item not found." };
        }

        if (unit.Status == LibraryUnitStatus.Broken)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "This item is broken and cannot be requested." };
        }

        if (unit.Status == LibraryUnitStatus.InTransit)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "This item is currently in transit." };
        }

        if (unit.CurrentPossessorUserId == userId)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "You cannot request an item you are holding." };
        }

        if (await libraryRepository.HasOpenRequestForUnitByUserAsync(unit.Id, userId, cancellationToken))
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "You already have an open request for this item." };
        }

        if (LibraryOfferingRules.IsOnDemand(unit.Offering))
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "On-demand offerings use record acquisition instead of requests." };
        }

        // Durables are always a single item; stock-based goods (incl. "N/A" stock
        // consumables) keep the requested quantity so gifts scale by amount.
        var quantity = LibraryOfferingRules.IsStockBased(unit.Offering) ? request.Quantity : 1;
        if (quantity < 1)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Quantity must be at least 1." };
        }

        if (LibraryOfferingRules.IsStockBased(unit.Offering)
            && !LibraryOfferingRules.HasSufficientStock(unit.Offering, quantity))
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Not enough stock available." };
        }

        // Consumables are "gone once you get it" — no borrowing window, so skip date
        // validation and pin the window to today. Durable/Service still validate dates.
        DateTime neededByStart;
        DateTime neededByEnd;
        if (unit.Offering.Kind == LibraryOfferingKind.Consumable)
        {
            var today = DateTime.SpecifyKind(DateTime.UtcNow.Date, DateTimeKind.Utc);
            neededByStart = today;
            neededByEnd = today;
        }
        else
        {
            var dateError = LibraryRequestValidation.ValidateDateRange(request.NeededByStart, request.NeededByEnd);
            if (dateError is not null)
            {
                return new LibraryRequestOperationResponse { Success = false, Message = dateError };
            }

            (neededByStart, neededByEnd) = LibraryRequestValidation.NormalizeDateRange(
                request.NeededByStart,
                request.NeededByEnd);
        }

        if (!LibraryOfferingRules.IsStockBased(unit.Offering)
            && await libraryRepository.HasOverlappingOpenRequestForUnitAsync(
                unit.Id,
                neededByStart,
                neededByEnd,
                cancellationToken: cancellationToken))
        {
            return new LibraryRequestOperationResponse
            {
                Success = false,
                Message = LibraryRequestValidation.OverlappingRequestDatesMessage
            };
        }

        var utcNow = DateTime.UtcNow;

        var libraryRequest = new LibraryRequest
        {
            UnitId = unit.Id,
            RequesterUserId = userId,
            Quantity = quantity,
            NeededByStart = neededByStart,
            NeededByEnd = neededByEnd,
            PurposePreview = LibraryRequestValidation.NormalizePurposePreview(request.PurposePreview),
            HasEncryptedContent = true,
            Status = LibraryRequestStatus.Open,
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

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = unit.CurrentPossessorUserId,
            CrewId = membership.CrewId,
            Kind = NotificationKind.NewLibraryRequest,
            Title = "New library request",
            Body = $"Someone requested {unit.Offering.Title}.",
            ActionUrl = $"/app/crew/library-of-things/requests/{libraryRequest.Id}",
            RelatedEntityId = libraryRequest.Id
        }, cancellationToken);

        return new LibraryRequestOperationResponse
        {
            Success = true,
            Message = "Request submitted.",
            RequestId = libraryRequest.Id
        };
    }
}
