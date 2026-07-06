namespace LiberationFleet.Server.Domain.Entities;

public class ForumPost
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime LastActivityAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }
    public bool IsAdultContent { get; set; }

    public Crew Crew { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public ICollection<ForumComment> Comments { get; set; } = new List<ForumComment>();
}
