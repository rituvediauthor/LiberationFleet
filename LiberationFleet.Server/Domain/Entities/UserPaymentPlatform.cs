namespace LiberationFleet.Server.Domain.Entities;

public class UserPaymentPlatform
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int PaymentPlatformId { get; set; }
    public PaymentPlatform PaymentPlatform { get; set; } = null!;
    public string Handle { get; set; } = string.Empty;
}
