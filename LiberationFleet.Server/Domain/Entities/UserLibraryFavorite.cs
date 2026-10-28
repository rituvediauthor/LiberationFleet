namespace LiberationFleet.Server.Domain.Entities;

public class UserLibraryFavorite
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int LibraryUnitId { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public LibraryUnit LibraryUnit { get; set; } = null!;
}
