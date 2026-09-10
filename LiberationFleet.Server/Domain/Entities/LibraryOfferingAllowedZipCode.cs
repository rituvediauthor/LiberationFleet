namespace LiberationFleet.Server.Domain.Entities;

public class LibraryOfferingAllowedZipCode
{
    public int OfferingId { get; set; }
    public string ZipCode { get; set; } = string.Empty;

    public LibraryOffering Offering { get; set; } = null!;
}
