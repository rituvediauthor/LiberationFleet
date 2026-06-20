namespace LiberationFleet.Server.Domain.Entities;

public class CrewPaymentPlatform
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public string Name { get; set; } = string.Empty;

    public Crew Crew { get; set; } = null!;
    public ICollection<UserPaymentPlatform> UserAccounts { get; set; } = new List<UserPaymentPlatform>();
    public ICollection<Gift> Gifts { get; set; } = new List<Gift>();
}
