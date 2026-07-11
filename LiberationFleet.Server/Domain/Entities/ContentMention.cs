using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class ContentMention
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int AuthorUserId { get; set; }
    public int MentionedUserId { get; set; }
    public MentionedContentType ContentType { get; set; }
    public int ResourceId { get; set; }
    public int? ParentResourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Crew Crew { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public User MentionedUser { get; set; } = null!;
}
