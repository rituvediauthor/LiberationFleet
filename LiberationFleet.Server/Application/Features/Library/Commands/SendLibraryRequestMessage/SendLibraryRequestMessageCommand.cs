using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.SendLibraryRequestMessage;

public record SendLibraryRequestMessageCommand(
    int RequestId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<LibraryRequestMessageOperationResponse>;

public class SendLibraryRequestMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<SendLibraryRequestMessageCommand, LibraryRequestMessageOperationResponse>
{
    public async Task<LibraryRequestMessageOperationResponse> Handle(
        SendLibraryRequestMessageCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryRequestMessageOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new LibraryRequestMessageOperationResponse { Success = false, Message = "Encrypted message content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryRequestMessageOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var libraryRequest = await libraryRepository.GetRequestByIdForCrewAsync(
            request.RequestId,
            membership.CrewId,
            cancellationToken);
        if (libraryRequest is null)
        {
            return new LibraryRequestMessageOperationResponse { Success = false, Message = "Request not found." };
        }

        if (!LibraryRequestAccess.CanMessage(libraryRequest, userId))
        {
            return new LibraryRequestMessageOperationResponse { Success = false, Message = "Messaging is not available for this request." };
        }

        var utcNow = DateTime.UtcNow;
        var message = new LibraryRequestMessage
        {
            RequestId = libraryRequest.Id,
            AuthorUserId = userId,
            CreatedAt = utcNow
        };

        await libraryRepository.AddRequestMessageAsync(message, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var envelope = new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.LibraryRequestMessage,
            ResourceId = message.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await cryptoRepository.UpsertEnvelopeAsync(envelope, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedMessage = await libraryRepository.GetRequestMessageByIdWithAuthorAsync(message.Id, cancellationToken);
        var recipientUserId = userId == libraryRequest.RequesterUserId
            ? libraryRequest.Unit.CurrentPossessorUserId
            : libraryRequest.RequesterUserId;

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = recipientUserId,
            CrewId = membership.CrewId,
            Kind = NotificationKind.NewLibraryRequestMessage,
            Title = "Library request message",
            Body = "You have a new message about a library request.",
            ActionUrl = $"/app/crew/library-of-things/requests/{libraryRequest.Id}/chat",
            RelatedEntityId = libraryRequest.Id
        }, cancellationToken);

        return new LibraryRequestMessageOperationResponse
        {
            Success = true,
            Message = "Message sent.",
            MessageId = message.Id,
            Item = savedMessage is null
                ? null
                : LibraryMapper.MapRequestMessage(savedMessage, envelope)
        };
    }
}
