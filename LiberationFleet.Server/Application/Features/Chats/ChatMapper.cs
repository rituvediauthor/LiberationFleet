using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Chats;

public static class ChatMapper
{
    public static ChatRoomListItemDto MapListItem(ChatRoom room, EncryptedContentEnvelope? nameEnvelope)
    {
        var dto = new ChatRoomListItemDto
        {
            Id = room.Id,
            RoomType = room.RoomType,
            CreatedByUserId = room.CreatedByUserId,
            CreatedByUsername = room.CreatedByUser?.Username ?? string.Empty,
            CreatedAt = room.CreatedAt,
            LastActivityAt = room.LastActivityAt
        };

        if (nameEnvelope is not null)
        {
            dto.HasEncryptedContent = true;
            dto.EncryptedPayload = CryptoMapper.MapPayload(nameEnvelope);
            dto.CreatedByUsername = string.Empty;
        }
        else
        {
            dto.Name = room.Name;
        }

        return dto;
    }

    public static ChatMessageDto MapMessage(ChatRoomMessage message, EncryptedContentEnvelope? envelope) => new()
    {
        Id = message.Id,
        AuthorUserId = message.AuthorUserId,
        AuthorUsername = envelope is null ? message.AuthorUser?.Username ?? string.Empty : string.Empty,
        CreatedAt = message.CreatedAt,
        HasEncryptedContent = envelope is not null,
        EncryptedPayload = envelope is null ? null : CryptoMapper.MapPayload(envelope)
    };
}
