namespace LiberationFleet.Server.Domain.Entities;

public class LibraryCategory
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }
}
