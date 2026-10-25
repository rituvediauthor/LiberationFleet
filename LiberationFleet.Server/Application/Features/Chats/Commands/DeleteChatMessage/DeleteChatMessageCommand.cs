using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.DeleteChatMessage;

public record DeleteChatMessageCommand(int RoomId, int MessageId) : IRequest<ChatOperationResponse>;

public class DeleteChatMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    IChatRealtimeNotifier chatRealtimeNotifier,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteChatMessageCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(DeleteChatMessageCommand request, CancellationToken cancellationToken)
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

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        var message = await chatRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.ChatRoomId != room.Id)
        {
            return new ChatOperationResponse { Success = false, Message = "Message not found." };
        }

        if (message.RemovedByModerator)
        {
            return new ChatOperationResponse { Success = false, Message = "This message has already been removed." };
        }

        if (message.IsDeleted)
        {
            return new ChatOperationResponse { Success = false, Message = "Message not found." };
        }

        var isAuthor = message.AuthorUserId == userId;
        var canModerate = CrewRoleAuthorizationService.CanModerateAttachments(membership);
        if (!isAuthor && !canModerate)
        {
            return new ChatOperationResponse { Success = false, Message = "Only the author can delete this message." };
        }

        if (isAuthor)
        {
            message.IsDeleted = true;
            message.RemovedByModerator = false;
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await chatRealtimeNotifier.NotifyMessageDeletedAsync(membership.CrewId, room.Id, message.Id, cancellationToken);

            return new ChatOperationResponse
            {
                Success = true,
                Message = "Message deleted.",
                MessageId = message.Id
            };
        }

        message.IsDeleted = true;
        message.RemovedByModerator = true;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var tombstone = ChatMapper.MapMessage(
            message,
            envelope: null,
            viewerUserId: null,
            allowKick: room.CrewId.HasValue);
        await chatRealtimeNotifier.NotifyMessageUpdatedAsync(membership.CrewId, room.Id, tombstone, cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = "Message removed.",
            MessageId = message.Id
        };
    }
}
