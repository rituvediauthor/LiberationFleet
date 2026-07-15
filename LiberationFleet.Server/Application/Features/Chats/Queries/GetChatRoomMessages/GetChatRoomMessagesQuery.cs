using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoomMessages;

public record GetChatRoomMessagesQuery(int RoomId, int Limit, int? BeforeMessageId) : IRequest<ChatMessageListResponse>;

public class GetChatRoomMessagesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository,
    IUserBlockRepository blockRepository) : IRequestHandler<GetChatRoomMessagesQuery, ChatMessageListResponse>
{
    private const int MaxLimit = 50;

    public async Task<ChatMessageListResponse> Handle(GetChatRoomMessagesQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ChatMessageListResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ChatMessageListResponse { Success = false, Message = "You are not in a crew." };
        }

        var room = await chatRepository.GetRoomByIdAsync(request.RoomId, cancellationToken);
        if (room is null || !await ChatRoomAccess.CanAccessRoomAsync(room, membership, fleetRepository, cancellationToken))
        {
            return new ChatMessageListResponse { Success = false, Message = "Chat room not found." };
        }

        var limit = request.Limit <= 0 ? MaxLimit : Math.Min(request.Limit, MaxLimit);
        var hiddenUserIds = await blockRepository.GetHiddenUserIdsForViewerAsync(userId, cancellationToken);
        var messages = request.BeforeMessageId.HasValue
            ? await chatRepository.GetMessagesBeforeIdAsync(request.RoomId, request.BeforeMessageId.Value, limit, cancellationToken)
            : await chatRepository.GetLatestMessagesAsync(request.RoomId, limit, cancellationToken);

        messages = messages
            .Where(m => !hiddenUserIds.Contains(m.AuthorUserId))
            .ToList();

        var envelopeById = new Dictionary<string, EncryptedContentEnvelope>(StringComparer.Ordinal);
        if (room.CrewId.HasValue)
        {
            var resourceIds = messages.Select(m => m.Id.ToString()).ToList();
            var envelopes = await cryptoRepository.GetEnvelopesAsync(
                EncryptedContentType.ChatRoomMessage,
                resourceIds,
                crewId: room.CrewId.Value,
                cancellationToken: cancellationToken);
            envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);
        }
        else if (room.FleetId.HasValue)
        {
            var resourceIds = messages.Select(m => m.Id.ToString()).ToList();
            var envelopes = await cryptoRepository.GetEnvelopesAsync(
                EncryptedContentType.ChatRoomMessage,
                resourceIds,
                fleetId: room.FleetId.Value,
                cancellationToken: cancellationToken);
            envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);
        }

        var hasMore = false;
        if (messages.Count > 0)
        {
            var oldestId = messages[0].Id;
            var older = await chatRepository.GetMessagesBeforeIdAsync(request.RoomId, oldestId, 1, cancellationToken);
            hasMore = older.Count > 0;
        }

        var items = messages.Select(message =>
        {
            envelopeById.TryGetValue(message.Id.ToString(), out var envelope);
            return ChatMapper.MapMessage(message, envelope);
        }).ToList();

        return new ChatMessageListResponse
        {
            Success = true,
            Message = "Messages loaded.",
            Items = items,
            HasMore = hasMore
        };
    }
}
