namespace LiberationFleet.Server.Domain.Entities;

public class GiftComment
{
    public int Id { get; set; }
    public int GiftId { get; set; }
    public int AuthorUserId { get; set; }
    public int? ParentCommentId { get; set; }
    public int? ReplyToCommentId { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public Gift Gift { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public GiftComment? ParentComment { get; set; }
    public GiftComment? ReplyToComment { get; set; }
    public ICollection<GiftComment> Replies { get; set; } = new List<GiftComment>();
}
