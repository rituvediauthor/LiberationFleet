using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetChatRooms;

public record GetFleetChatRoomsQuery : IRequest<ChatRoomListResponse>;

public class GetFleetChatRoomsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IUserRepository userRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetFleetChatRoomsQuery, ChatRoomListResponse>
{
    public async Task<ChatRoomListResponse> Handle(GetFleetChatRoomsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatRoomListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        var fleet = await fleetRepository.GetFleetForUserAsync(userId, cancellationToken);
        if (fleet is null)
        {
            return new ChatRoomListResponse { Success = false, Message = "You are not in a fleet." };
        }

        var rooms = await chatRepository.GetRoomsByFleetIdAsync(fleet.Id, cancellationToken);
        var personalOrder = await chatRepository.GetPersonalOrderAsync(
            userId,
            crewId: null,
            fleet.Id,
            cancellationToken);
        rooms = ApplyPersonalOrder(rooms, personalOrder?.OrderedRoomIdsJson);
        var user = await userRepository.GetByIdAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        rooms = rooms
            .Where(room => !AdultContentAccess.IsBlocked(preference, room.IsAdultContent))
            .ToList();

        var resourceIds = rooms.Select(room => room.Id.ToString()).ToList();
        var nameEnvelopes = await cryptoRepository.GetEnvelopesAsync(
            Domain.Enums.EncryptedContentType.ChatRoomName,
            resourceIds,
            fleetId: fleet.Id,
            cancellationToken: cancellationToken);
        var envelopeByRoomId = nameEnvelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

        var items = rooms.Select(room =>
        {
            envelopeByRoomId.TryGetValue(room.Id.ToString(), out var nameEnvelope);
            return ChatMapper.MapListItem(room, nameEnvelope, membership);
        }).ToList();

        return new ChatRoomListResponse
        {
            Success = true,
            Message = "Fleet chat rooms loaded.",
            Items = items
        };
    }

    private static IReadOnlyList<ChatRoom> ApplyPersonalOrder(
        IReadOnlyList<ChatRoom> rooms,
        string? orderedRoomIdsJson)
    {
        if (string.IsNullOrWhiteSpace(orderedRoomIdsJson))
        {
            return rooms;
        }

        try
        {
            var roomIds = JsonSerializer.Deserialize<int[]>(orderedRoomIdsJson) ?? [];
            var positionById = roomIds
                .Distinct()
                .Select((id, index) => new { id, index })
                .ToDictionary(item => item.id, item => item.index);
            return rooms
                .Select((room, repositoryIndex) => new { room, repositoryIndex })
                .OrderBy(item => positionById.TryGetValue(item.room.Id, out var index) ? index : int.MaxValue)
                .ThenBy(item => item.repositoryIndex)
                .Select(item => item.room)
                .ToList();
        }
        catch (JsonException)
        {
            return rooms;
        }
    }
}
