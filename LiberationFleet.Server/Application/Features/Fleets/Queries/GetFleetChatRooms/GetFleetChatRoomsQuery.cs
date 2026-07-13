using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetChatRooms;

public record GetFleetChatRoomsQuery : IRequest<ChatRoomListResponse>;

public class GetFleetChatRoomsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IUserRepository userRepository,
    IChatRepository chatRepository) : IRequestHandler<GetFleetChatRoomsQuery, ChatRoomListResponse>
{
    public async Task<ChatRoomListResponse> Handle(GetFleetChatRoomsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatRoomListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatRoomListResponse { Success = false, Message = "You are not in a crew." };
        }

        var fleet = await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken);
        if (fleet is null)
        {
            return new ChatRoomListResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var rooms = await chatRepository.GetRoomsByFleetIdAsync(fleet.Id, cancellationToken);
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        rooms = rooms
            .Where(room => !AdultContentAccess.IsBlocked(preference, room.IsAdultContent))
            .ToList();

        var items = rooms.Select(room => ChatMapper.MapListItem(room, nameEnvelope: null, membership)).ToList();

        return new ChatRoomListResponse
        {
            Success = true,
            Message = "Fleet chat rooms loaded.",
            Items = items
        };
    }
}
