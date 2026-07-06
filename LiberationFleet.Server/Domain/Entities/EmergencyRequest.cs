using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class EmergencyRequest
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int RequesterUserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    public decimal AmountFulfilled { get; set; }
    public EmergencyRequestStatus Status { get; set; } = EmergencyRequestStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

    public Crew Crew { get; set; } = null!;
    public User RequesterUser { get; set; } = null!;
    public ICollection<EmergencySplitOffer> SplitOffers { get; set; } = new List<EmergencySplitOffer>();
    public ICollection<EmergencyGiftResponse> GiftResponses { get; set; } = new List<EmergencyGiftResponse>();
}
