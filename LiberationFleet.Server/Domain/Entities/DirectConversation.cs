namespace LiberationFleet.Server.Domain.Entities;

public class DirectConversation
{
    public int Id { get; set; }
    public int UserLowId { get; set; }
    public int UserHighId { get; set; }
    public DateTime? LastMessageAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User UserLow { get; set; } = null!;
    public User UserHigh { get; set; } = null!;
    public ICollection<DirectMessage> Messages { get; set; } = new List<DirectMessage>();
}
