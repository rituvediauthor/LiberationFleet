using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Friends;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Friends.Commands.UpdateDirectMessage;

public record UpdateDirectMessageCommand(
    int FriendUserId,
    int MessageId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<DirectMessageOperationResponse>;

public class UpdateDirectMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFriendshipRepository friendshipRepository,
    IUserBlockRepository blockRepository,
    IUserRepository userRepository,
    IDirectMessageRepository directMessageRepository,
    ICryptoRepository cryptoRepository,
    IDirectMessageRealtimeNotifier directMessageRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateDirectMessageCommand, DirectMessageOperationResponse>
{
    public async Task<DirectMessageOperationResponse> Handle(UpdateDirectMessageCommand request, CancellationToken cancellationToken)
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

        var message = await directMessageRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.IsDeleted)
        {
            return new DirectMessageOperationResponse { Success = false, Message = "Message not found." };
        }

        if (message.AuthorUserId != access.ViewerId)
        {
            return new DirectMessageOperationResponse { Success = false, Message = "Only the author can edit this message." };
        }

        var conversation = await directMessageRepository.GetConversationBetweenUsersAsync(
            access.ViewerId,
            request.FriendUserId,
            cancellationToken);
        if (conversation is null || message.ConversationId != conversation.Id)
        {
            return new DirectMessageOperationResponse { Success = false, Message = "Message not found." };
        }

        var utcNow = DateTime.UtcNow;
        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
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
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var envelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.DirectMessage,
            message.Id.ToString(),
            cancellationToken);
        if (envelope is not null)
        {
            var messageDto = DirectMessageMapper.MapMessage(message, envelope);
            await directMessageRealtimeNotifier.NotifyDirectMessageUpdatedAsync(
                access.ViewerId,
                request.FriendUserId,
                messageDto,
                cancellationToken);
        }

        return new DirectMessageOperationResponse
        {
            Success = true,
            Message = "Message updated.",
            MessageId = message.Id
        };
    }
}
