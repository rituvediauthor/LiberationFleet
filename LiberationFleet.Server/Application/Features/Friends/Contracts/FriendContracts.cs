using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crypto.Contracts;

namespace LiberationFleet.Server.Application.Features.Friends.Contracts;

public class FriendListItemDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarResourceId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public bool IsMuted { get; set; }
}

public class FriendListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<FriendListItemDto> Items { get; set; } = Array.Empty<FriendListItemDto>();
}

public enum FriendRequestDirectionDto
{
    Incoming = 0,
    Outgoing = 1
}

public class FriendRequestListItemDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public string? AvatarResourceId { get; set; }
    public DateTime? LastLoginAt { get; set; }
    public FriendRequestDirectionDto Direction { get; set; }
    public DateTime CreatedAt { get; set; }
}

public class FriendRequestListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<FriendRequestListItemDto> Items { get; set; } = Array.Empty<FriendRequestListItemDto>();
}

public class BlockedUserListItemDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public DateTime BlockedAt { get; set; }
}

public class BlockedUserListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<BlockedUserListItemDto> Items { get; set; } = Array.Empty<BlockedUserListItemDto>();
}

public class UserSearchResultDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public CrewmateFriendshipStateDto FriendshipState { get; set; }
}

public class UserSearchResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<UserSearchResultDto> Items { get; set; } = Array.Empty<UserSearchResultDto>();
}

public class DirectMessageDto
{
    public int Id { get; set; }
    public int AuthorUserId { get; set; }
    public string AuthorUsername { get; set; } = string.Empty;
    public string? AuthorAvatarResourceId { get; set; }
    public DateTime CreatedAt { get; set; }
    public bool HasEncryptedContent { get; set; }
    public EncryptedPayloadDto? EncryptedPayload { get; set; }
}

public class DirectMessageListResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<DirectMessageDto> Items { get; set; } = Array.Empty<DirectMessageDto>();
    public bool HasMore { get; set; }
    public string FriendUsername { get; set; } = string.Empty;
}

public class DirectMessageOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? MessageId { get; set; }
}

public class SendDirectMessageRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}

public class UpdateDirectMessageRequest
{
    public string Nonce { get; set; } = string.Empty;
    public string Ciphertext { get; set; } = string.Empty;
    public int KeyVersion { get; set; } = 1;
}
