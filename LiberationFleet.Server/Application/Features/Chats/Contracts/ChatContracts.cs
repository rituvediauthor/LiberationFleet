using LiberationFleet.Server.Application.Features.Crypto.Contracts;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Chats.Contracts;

public class ChatRoomListItemDto
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
    public ChatRoomType RoomType { get; set; }
    public int CreatedByUserId { get; set; }
    public string CreatedByUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public DateTime LastActivityAt { get; set; }
}

public class ChatMessageDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public DateTime CreatedAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
}

public class ChatRoomListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ChatRoomListItemDto> Items { get; set; } = Array.Empty<ChatRoomListItemDto>();
}

public class ChatMessageListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<ChatMessageDto> Items { get; set; } = Array.Empty<ChatMessageDto>();
    public bool HasMore { get; set; }
}

public class ChatOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? RoomId { get; set; }
    public int? MessageId { get; set; }
}

public class CreateChatRoomRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
    public ChatRoomType RoomType { get; set; }
}

public class SendChatMessageRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}
