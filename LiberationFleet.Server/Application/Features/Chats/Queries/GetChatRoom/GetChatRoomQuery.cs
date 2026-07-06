using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoom;

public record GetChatRoomQuery(int RoomId) : IRequest<ChatRoomDetailResponse>;

public class GetChatRoomQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetChatRoomQuery, ChatRoomDetailResponse>
{
    public async Task<ChatRoomDetailResponse> Handle(GetChatRoomQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatRoomDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var room = await chatRepository.GetRoomByIdWithAuthorAsync(request.RoomId, cancellationToken);
        if (room is null)
        {
            return new ChatRoomDetailResponse { Success = false, Message = "Chat room not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, room.CrewId, cancellationToken))
        {
            return new ChatRoomDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatRoomDetailResponse { Success = false, Message = "You are not in this crew." };
        }

        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        if (AdultContentAccess.IsBlocked(preference, room.IsAdultContent))
        {
            return new ChatRoomDetailResponse { Success = false, Message = "Chat room not found." };
        }

        var nameEnvelope = await cryptoRepository.GetEnvelopeAsync(
            EncryptedContentType.ChatRoomName,
            room.Id.ToString(),
            cancellationToken);

        return new ChatRoomDetailResponse
        {
            Success = true,
            Message = "Chat room loaded.",
            Room = ChatMapper.MapDetail(room, nameEnvelope, membership)
        };
    }
}
