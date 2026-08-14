namespace LiberationFleet.Server.Domain.Entities;

public class UserChatChannelOrder
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? CrewId { get; set; }
    public int? FleetId { get; set; }
    public string OrderedRoomIdsJson { get; set; } = "[]";
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Crew? Crew { get; set; }
    public Fleet? Fleet { get; set; }
}
