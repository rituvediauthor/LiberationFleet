namespace LiberationFleet.Server.Domain.Entities;

public class VoiceParticipantSession
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int CrewId { get; set; }
    public int ChatRoomId { get; set; }
    public string? ConnectionId { get; set; }
    public bool IsMuted { get; set; }
    public bool IsDeafened { get; set; }
    public bool IsSpeaking { get; set; }
    public bool IsServerMuted { get; set; }
    public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastSeenAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public ChatRoom ChatRoom { get; set; } = null!;
}
