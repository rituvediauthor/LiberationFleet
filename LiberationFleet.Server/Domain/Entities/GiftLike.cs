namespace LiberationFleet.Server.Domain.Entities;

public class GiftLike
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int? GiftId { get; set; }
    public int? GiftCommentId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime? RemovedAt { get; set; }
    public bool AuthorNotified { get; set; }

    public User? User { get; set; }
    public Gift? Gift { get; set; }
    public GiftComment? GiftComment { get; set; }
}
