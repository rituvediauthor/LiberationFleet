using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class LibraryOffering
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int CreatorUserId { get; set; }
    public LibraryOfferingKind Kind { get; set; }
    public LibraryFulfillmentMode FulfillmentMode { get; set; }
    public string Title { get; set; } = string.Empty;
    public string TitleNormalized { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public decimal ValuePerUnit { get; set; }
    public string? UnitLabel { get; set; }
    public int? RemainingStock { get; set; }
    public bool QuantityNotApplicable { get; set; }
    public bool IsOutOfStock { get; set; }
    public string? ThumbnailResourceId { get; set; }
    public bool HasEncryptedContent { get; set; }
    public bool IsDeleted { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    public DateTime UpdatedAt { get; set; } = DateTime.UtcNow;

    public Crew Crew { get; set; } = null!;
    public User CreatorUser { get; set; } = null!;
    public ICollection<LibraryUnit> Units { get; set; } = new List<LibraryUnit>();
    public ICollection<LibraryOfferingCategory> Categories { get; set; } = new List<LibraryOfferingCategory>();
}
