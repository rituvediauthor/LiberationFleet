using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Commands.SendDirectMessage;

public record SendDirectMessageCommand(
    int FriendUserId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<DirectMessageOperationResponse>;

public class SendDirectMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUserRepository userRepository,
    IDirectMessageRepository directMessageRepository,
    ICryptoRepository cryptoRepository,
    IDirectMessageRealtimeNotifier directMessageRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<SendDirectMessageCommand, DirectMessageOperationResponse>
{
    public async Task<DirectMessageOperationResponse> Handle(SendDirectMessageCommand request, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new DirectMessageOperationResponse { Success = false, Message = "Encrypted message content is required." };
        }

        var access = await FriendAccessHelper.ValidateFriendMessagingAsync(
            currentUser,
            membershipRepository,
            friendshipRepository,
            blockRepository,
            userRepository,
            request.FriendUserId,
            cancellationToken);
        if (!access.Success)
        {
            return new DirectMessageOperationResponse { Success = false, Message = access.Message };
        }

        var utcNow = DateTime.UtcNow;
        var conversation = await directMessageRepository.GetOrCreateConversationAsync(
            access.ViewerId,
            request.FriendUserId,
            cancellationToken);

        var message = new DirectMessage
        {
            AuthorUserId = access.ViewerId,
            CreatedAt = utcNow,
            Conversation = conversation
        };

        await directMessageRepository.AddMessageAsync(message, cancellationToken);
        conversation.LastMessageAt = utcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var envelope = new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.DirectMessage,
            ResourceId = message.Id.ToString(),
            CrewId = access.CrewId,
            AuthorUserId = access.ViewerId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        };

        await cryptoRepository.UpsertEnvelopeAsync(envelope, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var savedMessage = await directMessageRepository.GetMessageByIdWithAuthorAsync(message.Id, cancellationToken);
        if (savedMessage is not null)
        {
            var messageDto = DirectMessageMapper.MapMessage(savedMessage, envelope);
            await directMessageRealtimeNotifier.NotifyDirectMessageSentAsync(
                access.ViewerId,
                request.FriendUserId,
                messageDto,
                cancellationToken);
        }

        return new DirectMessageOperationResponse
        {
            Success = true,
            Message = "Message sent.",
            MessageId = message.Id
        };
    }
}
