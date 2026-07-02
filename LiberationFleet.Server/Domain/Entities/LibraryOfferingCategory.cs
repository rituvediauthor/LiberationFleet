namespace LiberationFleet.Server.Domain.Entities;

public class LibraryOfferingCategory
{
    public int OfferingId { get; set; }
    public int CategoryId { get; set; }

    public LibraryOffering Offering { get; set; } = null!;
    public LibraryCategory Category { get; set; } = null!;
}
