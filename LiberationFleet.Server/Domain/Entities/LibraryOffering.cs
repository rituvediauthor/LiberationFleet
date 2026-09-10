using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class LibraryOffering
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int CreatorUserId { get; set; }
    public LibraryOfferingKind Kind { get; set; }
    public LibraryFulfillmentMode FulfillmentMode { get; set; }
    public LibraryOfferingVisibility Visibility { get; set; } = LibraryOfferingVisibility.CrewOnly;
    public string Title { get; set; } = string.Empty;
    public string TitleNormalized { get; set; } = string.Empty;
    public string DescriptionPreview { get; set; } = string.Empty;
    public decimal ValuePerUnit { get; set; }
    public string? UnitLabel { get; set; }
    public int? RemainingStock { get; set; }
    public int? RemainingStockTier1 { get; set; }
    public int? RemainingStockTier2 { get; set; }
    public int? RemainingStockTier3 { get; set; }
    public int? RemainingStockTier4 { get; set; }
    public int? RemainingStockTier5 { get; set; }
    public int? RemainingStockTier6 { get; set; }
    /// <summary>Services: minimum LoT priority tier that may view/request this offering (1–6).</summary>
    public int MinimumViewerTier { get; set; } = 1;
    /// <summary>ISO 3166-1 alpha-2; required when <see cref="AllowedZipCodes"/> is non-empty.</summary>
    public string? CountryCode { get; set; }
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
    /// <summary>
    /// When empty, no zip gate. When non-empty, the viewer’s profile zip must be listed.
    /// </summary>
    public ICollection<LibraryOfferingAllowedZipCode> AllowedZipCodes { get; set; } = new List<LibraryOfferingAllowedZipCode>();
}
