using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class UserHiddenContent
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public MutedContentType ContentType { get; set; }
    public int ResourceId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
