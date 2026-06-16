namespace LiberationFleet.Server.Domain.Entities;

public class PaymentPlatform
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public int SortOrder { get; set; }

    public ICollection<UserPaymentPlatform> UserAccounts { get; set; } = new List<UserPaymentPlatform>();
    public ICollection<Gift> Gifts { get; set; } = new List<Gift>();
}
