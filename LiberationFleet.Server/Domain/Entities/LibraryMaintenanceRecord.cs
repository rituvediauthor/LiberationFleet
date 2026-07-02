namespace LiberationFleet.Server.Domain.Entities;

public class LibraryMaintenanceRecord
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public int ContributorUserId { get; set; }
    public decimal Cost { get; set; }
    public bool HasEncryptedContent { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LibraryUnit Unit { get; set; } = null!;
    public User ContributorUser { get; set; } = null!;
}
