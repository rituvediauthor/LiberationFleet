using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Crypto;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Chats;

public static class ChatMapper
{
    public static ChatRoomListItemDto MapListItem(
        ChatRoom room,
        EncryptedContentEnvelope? nameEnvelope,
        CrewMembership? viewerMembership = null)
    {
        var dto = new ChatRoomListItemDto
        {
            Id = room.Id,
            Purpose = room.Purpose,
            RoomType = room.RoomType,
            CreatedByUserId = room.CreatedByUserId,
            CreatedByUsername = room.CreatedByUser?.Username ?? string.Empty,
            CreatedAt = room.CreatedAt,
            LastActivityAt = room.LastActivityAt,
            AnonymousModeEnabled = room.AnonymousModeEnabled,
            CanToggleAnonymousMode = viewerMembership is not null
                && CrewRoleAuthorizationService.CanToggleAnonymousChat(viewerMembership),
            IsAdultContent = room.IsAdultContent
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

    public static ChatRoomDetailDto MapDetail(
        ChatRoom room,
        EncryptedContentEnvelope? nameEnvelope,
        CrewMembership? viewerMembership = null)
    {
        var item = MapListItem(room, nameEnvelope, viewerMembership);
        return new ChatRoomDetailDto
        {
            Id = item.Id,
            Name = item.Name,
            Purpose = item.Purpose,
            HasEncryptedContent = item.HasEncryptedContent,
            EncryptedPayload = item.EncryptedPayload,
            RoomType = item.RoomType,
            CreatedByUserId = item.CreatedByUserId,
            CreatedByUsername = item.CreatedByUsername,
            CreatedAt = item.CreatedAt,
            LastActivityAt = item.LastActivityAt,
            AnonymousModeEnabled = item.AnonymousModeEnabled,
            CanToggleAnonymousMode = item.CanToggleAnonymousMode,
            IsAdultContent = item.IsAdultContent
        };
    }

    public static ChatMessageDto MapMessage(
        ChatRoomMessage message,
        EncryptedContentEnvelope? envelope,
        int? viewerUserId = null,
        bool allowKick = false,
        IReadOnlySet<int>? crewAvatarAllowedUserIds = null)
    {
        var isOwn = viewerUserId.HasValue && message.AuthorUserId == viewerUserId.Value;
        var hideIdentity = message.IsAnonymous && !isOwn;

        return new ChatMessageDto
        {
            Id = message.Id,
            AuthorUserId = hideIdentity ? 0 : message.AuthorUserId,
            AuthorUsername = hideIdentity
                ? "Anonymous"
                : message.IsAnonymous
                    ? "Anonymous"
                    : envelope is null
                        ? message.AuthorUser?.Username ?? string.Empty
                        : string.Empty,
            AuthorAvatarResourceId = hideIdentity || message.IsAnonymous
                ? null
                : CrewAvatarVisibilityService.Filter(
                    message.AuthorUser?.AvatarResourceId,
                    message.AuthorUserId,
                    crewAvatarAllowedUserIds),
            CreatedAt = message.CreatedAt,
            HasEncryptedContent = envelope is not null,
            EncryptedPayload = envelope is null ? null : CryptoMapper.MapPayload(envelope),
            Body = envelope is null ? message.Body : null,
            IsAnonymous = message.IsAnonymous,
            CanKick = allowKick && message.IsAnonymous && !isOwn
        };
    }
}
