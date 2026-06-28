namespace LiberationFleet.Server.Domain.Entities;

public class DirectMessage
{
    public int Id { get; set; }
    public int ConversationId { get; set; }
    public int AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public DirectConversation Conversation { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}
