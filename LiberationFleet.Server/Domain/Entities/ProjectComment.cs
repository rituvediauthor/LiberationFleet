namespace LiberationFleet.Server.Domain.Entities;

public class ProjectComment
{
    public int Id { get; set; }
    public int ProjectPostId { get; set; }
    public int AuthorUserId { get; set; }
    public int? ParentCommentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public ProjectPost ProjectPost { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public ProjectComment? ParentComment { get; set; }
    public ICollection<ProjectComment> Replies { get; set; } = new List<ProjectComment>();
}
