using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ChatRoom
{
    public int Id { get; set; }
    public int? CrewId { get; set; }
    public int? FleetId { get; set; }
    public int? LinkedCrewId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Purpose { get; set; } = string.Empty;
    public ChatRoomType RoomType { get; set; }
    public int CreatedByUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public bool AnonymousModeEnabled { get; set; }
    public bool IsAdultContent { get; set; }

    public Crew? Crew { get; set; }
    public Fleet? Fleet { get; set; }
    public Crew? LinkedCrew { get; set; }
    public User CreatedByUser { get; set; } = null!;
    public ICollection<ChatRoomMessage> Messages { get; set; } = new List<ChatRoomMessage>();
}
