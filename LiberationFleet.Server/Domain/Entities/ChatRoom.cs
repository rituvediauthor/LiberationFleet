using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ChatRoom
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public string Name { get; set; } = string.Empty;
    public ChatRoomType RoomType { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public Crew Crew { get; set; } = null!;
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ChatRoomMessage> Messages { get; set; } = new List<ChatRoomMessage>();
}
