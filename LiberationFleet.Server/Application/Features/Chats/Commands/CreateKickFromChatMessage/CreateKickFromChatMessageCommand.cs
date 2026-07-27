using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.CreateKickFromChatMessage;

public record CreateKickFromChatMessageCommand(int RoomId, int MessageId, string Reason)
    : IRequest<ChatOperationResponse>;

public class CreateKickFromChatMessageCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    CrewmateKickProposalService kickProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateKickFromChatMessageCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(
        CreateKickFromChatMessageCommand request,
        CancellationToken cancellationToken)
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
        if (room is null || !room.CrewId.HasValue
            || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        if (room.CrewId.Value != membership.CrewId)
        {
            return new ChatOperationResponse { Success = false, Message = "Kick proposals are only available in your crew chats." };
        }

        var message = await chatRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.ChatRoomId != room.Id || message.IsDeleted)
        {
            return new ChatOperationResponse { Success = false, Message = "Message not found." };
        }

        if (!message.IsAnonymous)
        {
            return new ChatOperationResponse
            {
                Success = false,
                Message = "Kick-from-chat is only available for anonymous messages."
            };
        }

        if (message.AuthorUserId == userId)
        {
            return new ChatOperationResponse { Success = false, Message = "You cannot kick yourself." };
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new ChatOperationResponse { Success = false, Message = "A reason is required to submit a kick proposal." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(message.AuthorUserId, membership.CrewId, cancellationToken))
        {
            return new ChatOperationResponse { Success = false, Message = "That crewmate is no longer in the crew." };
        }

        var kickResult = await kickProposalService.CreateFromAnonymousChatMessageAsync(
            membership.CrewId,
            userId,
            message.AuthorUserId,
            message.Id,
            request.Reason.Trim(),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatOperationResponse
        {
            Success = kickResult.Success,
            Message = kickResult.Message,
            ProposalId = kickResult.ProposalId
        };
    }
}
