using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoomMessages;

public record GetChatRoomMessagesQuery(int RoomId, int Limit, int? BeforeMessageId) : IRequest<ChatMessageListResponse>;

public class GetChatRoomMessagesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IChatRepository chatRepository,
    ICryptoRepository cryptoRepository) : IRequestHandler<GetChatRoomMessagesQuery, ChatMessageListResponse>
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

        if (!await chatRepository.RoomBelongsToCrewAsync(request.RoomId, membership.CrewId, cancellationToken))
        {
            return new ChatMessageListResponse { Success = false, Message = "Chat room not found." };
        }

        var limit = request.Limit <= 0 ? MaxLimit : Math.Min(request.Limit, MaxLimit);
        var messages = request.BeforeMessageId.HasValue
            ? await chatRepository.GetMessagesBeforeIdAsync(request.RoomId, request.BeforeMessageId.Value, limit, cancellationToken)
            : await chatRepository.GetLatestMessagesAsync(request.RoomId, limit, cancellationToken);

        var resourceIds = messages.Select(m => m.Id.ToString()).ToList();
        var envelopes = await cryptoRepository.GetEnvelopesAsync(
            EncryptedContentType.ChatRoomMessage,
            resourceIds,
            membership.CrewId,
            cancellationToken);
        var envelopeById = envelopes.ToDictionary(e => e.ResourceId, StringComparer.Ordinal);

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
