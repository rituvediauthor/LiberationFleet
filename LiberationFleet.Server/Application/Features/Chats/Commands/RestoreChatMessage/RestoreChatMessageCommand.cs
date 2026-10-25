using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.RestoreChatMessage;

public record RestoreChatMessageCommand(int RoomId, int MessageId) : IRequest<ChatOperationResponse>;

public class RestoreChatMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<RestoreChatMessageCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(RestoreChatMessageCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!CrewRoleAuthorizationService.CanModerateAttachments(membership))
        {
            return new ChatOperationResponse { Success = false, Message = "You do not have permission to restore this message." };
        }

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        var message = await chatRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.ChatRoomId != room.Id || !message.RemovedByModerator)
        {
            return new ChatOperationResponse { Success = false, Message = "Message not found." };
        }

        message.IsDeleted = false;
        message.RemovedByModerator = false;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var envelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ChatRoomMessage,
            message.Id.ToString(),
            cancellationToken);
        var messageDto = ChatMapper.MapMessage(message, envelope, viewerUserId: null, allowKick: room.CrewId.HasValue);
        await chatRealtimeNotifier.NotifyMessageUpdatedAsync(membership.CrewId, room.Id, messageDto, cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Message restored.",
            MessageId = message.Id
        };
    }
}
