namespace LiberationFleet.Server.Domain.Entities;

public class ProposalComment
{
    public int Id { get; set; }
    public int ProposalId { get; set; }
    public int AuthorUserId { get; set; }
    public int? ParentCommentId { get; set; }
    public int? ReplyToCommentId { get; set; }
    public string? Body { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public bool IsDeleted { get; set; }

    public Proposal Proposal { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
    public ProposalComment? ParentComment { get; set; }
    public ICollection<ProposalComment> Replies { get; set; } = new List<ProposalComment>();
}
