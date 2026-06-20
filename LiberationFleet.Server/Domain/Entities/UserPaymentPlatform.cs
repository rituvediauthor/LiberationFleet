namespace LiberationFleet.Server.Domain.Entities;

public class UserPaymentPlatform
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public User User { get; set; } = null!;
    public int CrewPaymentPlatformId { get; set; }
    public CrewPaymentPlatform CrewPaymentPlatform { get; set; } = null!;
    public string Handle { get; set; } = string.Empty;
    public bool IsPreferred { get; set; }
}
