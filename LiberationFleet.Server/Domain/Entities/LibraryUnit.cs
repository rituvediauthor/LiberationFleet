using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class LibraryUnit
{
    public int Id { get; set; }
    public int OfferingId { get; set; }
    public int CurrentPossessorUserId { get; set; }
    public LibraryUnitStatus Status { get; set; } = LibraryUnitStatus.Available;
    public bool BrokenPendingConfirmation { get; set; }
    public bool IsRetired { get; set; }
    public DateTime? BrokenReportedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public LibraryOffering Offering { get; set; } = null!;
    public User CurrentPossessorUser { get; set; } = null!;
    public ICollection<LibraryRequest> Requests { get; set; } = new List<LibraryRequest>();
    public ICollection<LibraryMaintenanceRecord> MaintenanceRecords { get; set; } = new List<LibraryMaintenanceRecord>();
}
