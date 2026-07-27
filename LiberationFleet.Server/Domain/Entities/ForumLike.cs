namespace LiberationFleet.Server.Domain.Entities;

public class ForumLike
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? ForumPostId { get; set; }
    public int? ForumCommentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
    public bool AuthorNotified { get; set; }

    public User? User { get; set; }
    public ForumPost? ForumPost { get; set; }
    public ForumComment? ForumComment { get; set; }
}
