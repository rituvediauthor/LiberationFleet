using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Commands.ToggleAnonymousMode;

public record ToggleAnonymousModeCommand(int RoomId, bool Enabled) : IRequest<ChatOperationResponse>;

public class ToggleAnonymousModeCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleAnonymousModeCommand, ChatOperationResponse>
{
    public async Task<ChatOperationResponse> Handle(ToggleAnonymousModeCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var room = await chatRepository.GetRoomByIdWithAuthorAsync(request.RoomId, cancellationToken);
        if (room is null || room.IsDeleted)
        {
            return new ChatOperationResponse { Success = false, Message = "Chat room not found." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null || membership.CrewId != room.CrewId)
        {
            return new ChatOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        if (!CrewRoleAuthorizationService.CanToggleAnonymousChat(membership))
        {
            return new ChatOperationResponse { Success = false, Message = "You do not have permission to toggle anonymous mode." };
        }

        room.AnonymousModeEnabled = request.Enabled;
        room.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ChatOperationResponse
        {
            Success = true,
            Message = request.Enabled ? "Anonymous mode enabled." : "Anonymous mode disabled.",
            RoomId = room.Id
        };
    }
}
