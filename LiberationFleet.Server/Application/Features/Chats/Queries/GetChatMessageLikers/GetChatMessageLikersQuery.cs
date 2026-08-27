using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Engagement.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Queries.GetChatMessageLikers;

public record GetChatMessageLikersQuery(int RoomId, int MessageId) : IRequest<ContentLikersResponse>;

public class GetChatMessageLikersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    CrewAvatarVisibilityService crewAvatarVisibility) : IRequestHandler<GetChatMessageLikersQuery, ContentLikersResponse>
{
    public async Task<ContentLikersResponse> Handle(
        GetChatMessageLikersQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ContentLikersResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ContentLikersResponse { Success = false, Message = "You are not in a crew." };
        }

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ContentLikersResponse { Success = false, Message = "Chat room not found." };
        }

        var message = await chatRepository.GetMessageByIdWithAuthorAsync(request.MessageId, cancellationToken);
        if (message is null || message.ChatRoomId != room.Id)
        {
            return new ContentLikersResponse { Success = false, Message = "Message not found." };
        }

        var likers = await chatRepository.GetActiveMessageLikersAsync(message.Id, cancellationToken);
        var avatarCrewId = room.CrewId ?? membership.CrewId;
        var avatarAllowed = await crewAvatarVisibility.GetUsersAllowedToShowCrewAvatarAsync(
            avatarCrewId,
            cancellationToken);

        return new ContentLikersResponse
        {
            Success = true,
            Message = "Likers loaded.",
            Items = likers.Select(l => new ContentLikerDto
            {
                UserId = l.UserId,
                Username = l.Username,
                AvatarResourceId = CrewAvatarVisibilityService.Filter(l.AvatarResourceId, l.UserId, avatarAllowed)
            }).ToList()
        };
    }
}
