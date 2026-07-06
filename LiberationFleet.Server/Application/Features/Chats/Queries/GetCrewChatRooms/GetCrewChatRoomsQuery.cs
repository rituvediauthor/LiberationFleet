using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Queries.GetCrewChatRooms;

public record GetCrewChatRoomsQuery() : IRequest<ChatRoomListResponse>;

public class GetCrewChatRoomsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetCrewChatRoomsQuery, ChatRoomListResponse>
{
    public async Task<ChatRoomListResponse> Handle(GetCrewChatRoomsQuery request, CancellationToken cancellationToken)
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

        var rooms = await chatRepository.GetRoomsByCrewIdAsync(membership.CrewId, cancellationToken);
        var resourceIds = rooms.Select(r => r.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ChatRoomName,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = rooms.Select(room =>
        {
            envelopeById.TryGetValue(room.Id.ToString(), out var envelope);
            return ChatMapper.MapListItem(room, envelope, membership);
        }).ToList();

        return new ChatRoomListResponse
        {
            Success = true,
            Message = "Chat rooms loaded.",
            Items = items
        };
    }
}
