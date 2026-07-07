namespace LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;

public class VoiceJoinResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public string? WsUrl { get; set; }
    public string? LiveKitRoomName { get; set; }
    public int? SessionId { get; set; }
    public int? ChatRoomId { get; set; }
    public int? PreviousChatRoomId { get; set; }
}

public class VoiceOperationResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class VoiceParticipantDto
{
    public int SessionId { get; set; }
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public int ChatRoomId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public bool IsServerMuted { get; set; }
    public DateTime JoinedAt { get; set; }
}

public class VoiceRoomPresenceDto
{
    public int ChatRoomId { get; set; }
    public IReadOnlyList<VoiceParticipantDto> Participants { get; set; } = Array.Empty<VoiceParticipantDto>();
}

public class VoicePresenceSnapshotResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public IReadOnlyList<VoiceRoomPresenceDto> Rooms { get; set; } = Array.Empty<VoiceRoomPresenceDto>();
}

public class UpdateVoiceStateRequest
{
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
}

public class VoiceDisconnectRequest
{
    public int UserId { get; set; }
}

public class VoiceServerMuteRequest
{
    public int UserId { get; set; }
    public bool IsServerMuted { get; set; }
}
