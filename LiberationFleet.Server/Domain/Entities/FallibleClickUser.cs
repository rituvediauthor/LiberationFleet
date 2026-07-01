namespace LiberationFleet.Server.Domain.Entities;

public class FallibleClickUser
{
    public int UserId { get; set; }
    public DateTime FirstClickedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
}
