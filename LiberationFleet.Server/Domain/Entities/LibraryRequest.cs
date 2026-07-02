using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class LibraryRequest
{
    public int Id { get; set; }
    public int UnitId { get; set; }
    public int RequesterUserId { get; set; }
    public int Quantity { get; set; } = 1;
    public DateTime NeededByStart { get; set; }
    public DateTime NeededByEnd { get; set; }
    public string PurposePreview { get; set; } = string.Empty;
    public bool HasEncryptedContent { get; set; }
    public LibraryRequestStatus Status { get; set; } = LibraryRequestStatus.Open;
    public DateTime? DeniedAt { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public LibraryUnit Unit { get; set; } = null!;
    public User RequesterUser { get; set; } = null!;
    public ICollection<LibraryRequestMessage> Messages { get; set; } = new List<LibraryRequestMessage>();
}
