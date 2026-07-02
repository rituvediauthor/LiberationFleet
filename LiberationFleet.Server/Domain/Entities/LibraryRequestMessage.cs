namespace LiberationFleet.Server.Domain.Entities;

public class LibraryRequestMessage
{
    public int Id { get; set; }
    public int RequestId { get; set; }
    public int AuthorUserId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LibraryRequest Request { get; set; } = null!;
    public User AuthorUser { get; set; } = null!;
}
