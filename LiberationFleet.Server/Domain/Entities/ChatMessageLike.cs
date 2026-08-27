namespace LiberationFleet.Server.Domain.Entities;

public class ChatMessageLike
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int ChatRoomMessageId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
    public bool AuthorNotified { get; set; }

    public User? User { get; set; }
    public ChatRoomMessage? ChatRoomMessage { get; set; }
}
