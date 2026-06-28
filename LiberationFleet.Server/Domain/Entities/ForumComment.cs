namespace LiberationFleet.Server.Domain.Entities;

public class ForumComment
{
    public int Id { get; set; }
    public int ForumPostId { get; set; }
    public int AuthorUserId { get; set; }
    public int? ParentCommentId { get; set; }
    public int? ReplyToCommentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public ForumPost ForumPost { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public ForumComment? ParentComment { get; set; }
    public ICollection<ForumComment> Replies { get; set; } = new List<ForumComment>();
}
