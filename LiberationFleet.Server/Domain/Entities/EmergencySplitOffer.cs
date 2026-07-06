namespace LiberationFleet.Server.Domain.Entities;

public class EmergencySplitOffer
{
    public int Id { get; set; }
    public int EmergencyRequestId { get; set; }
    public int OffererUserId { get; set; }
    public decimal Amount { get; set; }
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public EmergencyRequest EmergencyRequest { get; set; } = null!;
    public User OffererUser { get; set; } = null!;
}
