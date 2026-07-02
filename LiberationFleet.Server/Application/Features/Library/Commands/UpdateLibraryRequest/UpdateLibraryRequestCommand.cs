using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.UpdateLibraryRequest;

public record UpdateLibraryRequestCommand(
    int RequestId,
    string PurposePreview,
    DateTime NeededByStart,
    DateTime NeededByEnd,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryRequestOperationResponse>;

public class UpdateLibraryRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateLibraryRequestCommand, LibraryRequestOperationResponse>
{
    public async Task<LibraryRequestOperationResponse> Handle(
        UpdateLibraryRequestCommand request,
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

        var dateError = LibraryRequestValidation.ValidateDateRange(request.NeededByStart, request.NeededByEnd);
        if (dateError is not null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = dateError };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var libraryRequest = await libraryRepository.GetTrackedRequestByIdForRequesterAsync(
            request.RequestId,
            userId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Request not found." };
        }

        var unit = await libraryRepository.GetUnitByIdForCrewAsync(
            libraryRequest.UnitId,
            membership.CrewId,
            cancellationToken);
        if (unit is null)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Item not found." };
        }

        if (libraryRequest.Status != LibraryRequestStatus.Open)
        {
            return new LibraryRequestOperationResponse { Success = false, Message = "Only open requests can be edited." };
        }

        var (neededByStart, neededByEnd) = LibraryRequestValidation.NormalizeDateRange(
            request.NeededByStart,
            request.NeededByEnd);

        if (!LibraryOfferingRules.IsStockBased(unit.Offering)
            && await libraryRepository.HasOverlappingOpenRequestForUnitAsync(
                libraryRequest.UnitId,
                neededByStart,
                neededByEnd,
                libraryRequest.Id,
                cancellationToken))
        {
            return new LibraryRequestOperationResponse
            {
                Success = false,
                Message = LibraryRequestValidation.OverlappingRequestDatesMessage
            };
        }

        var utcNow = DateTime.UtcNow;

        libraryRequest.PurposePreview = LibraryRequestValidation.NormalizePurposePreview(request.PurposePreview);
        libraryRequest.NeededByStart = neededByStart;
        libraryRequest.NeededByEnd = neededByEnd;
        libraryRequest.UpdatedAt = utcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryRequest,
            ResourceId = libraryRequest.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = libraryRequest.CreatedAt,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryRequestOperationResponse
        {
            Success = true,
            Message = "Request updated.",
            RequestId = libraryRequest.Id
        };
    }
}
