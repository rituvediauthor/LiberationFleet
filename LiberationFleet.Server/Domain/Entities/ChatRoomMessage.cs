namespace LiberationFleet.Server.Domain.Entities;

public class ChatRoomMessage
{
    public int Id { get; set; }
    public int ChatRoomId { get; set; }
    public int AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    /// <summary>
    /// Plaintext body used for fleet chat rooms, which span multiple crews and therefore
    /// cannot use a single crew's E2E encryption key. Crew room messages keep using
    /// encrypted envelopes and leave this null.
    /// </summary>
    public string? Body { get; set; }

    public ChatRoom ChatRoom { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}
