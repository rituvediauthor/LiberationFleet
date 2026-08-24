namespace LiberationFleet.Server.Domain.Entities;

public class CrewPaymentPlatform
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public string Name { get; set; } = string.Empty;

    /// <summary>
    /// System-managed in-kind platform for Library of Things contribution gifts.
    /// Prefer this flag over name string matching in queries and aggregations.
    /// </summary>
    public bool IsLibraryOfThings { get; set; }

    public Crew Crew { get; set; } = null!;
    public ICollection<UserPaymentPlatform> UserAccounts { get; set; } = new List<UserPaymentPlatform>();
    public ICollection<Gift> Gifts { get; set; } = new List<Gift>();
}
