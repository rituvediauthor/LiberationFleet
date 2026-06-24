namespace LiberationFleet.Server.Domain.Entities;

public class ChatRoomMessage
{
    public int Id { get; set; }
    public int ChatRoomId { get; set; }
    public int AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public ChatRoom ChatRoom { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}
