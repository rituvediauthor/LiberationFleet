using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;
using System.Text.Json;

namespace LiberationFleet.Server.Application.Features.Chats.Queries.GetCrewChatRooms;

public record GetCrewChatRoomsQuery() : IRequest<ChatRoomListResponse>;

public class GetCrewChatRoomsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
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
        var personalOrder = await chatRepository.GetPersonalOrderAsync(
            userId,
            membership.CrewId,
            fleetId: null,
            cancellationToken);
        rooms = ApplyPersonalOrder(rooms, personalOrder?.OrderedRoomIdsJson);
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var preference = user?.AdultContentPreference ?? AdultContentPreference.Block;
        rooms = rooms
            .Where(room => !AdultContentAccess.IsBlocked(preference, room.IsAdultContent))
            .ToList();

        var resourceIds = rooms.Select(r => r.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ChatRoomName,
            resourceIds,
            crewId: membership.CrewId,
            cancellationToken: cancellationToken);
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
