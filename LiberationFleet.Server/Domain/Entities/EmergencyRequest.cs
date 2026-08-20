using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Domain.Entities;

public class EmergencyRequest
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int RequesterUserId { get; set; }
    public string Purpose { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    /// <summary>Direct gifts and queue-funded receipts applied toward the need.</summary>
    public decimal AmountReceived { get; set; }
    /// <summary>Sum of active split commitments (denormalized; kept in sync with split offers).</summary>
    public decimal AmountSplitCommitted { get; set; }
    public EmergencyRequestStatus Status { get; set; } = EmergencyRequestStatus.Open;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    /// <summary>
    /// Comma-separated user IDs who were locked leader/runner-up when this request was created.
    /// Empty/null means legacy: evaluate using live queue order at split time.
    /// </summary>
    public string? SplitEligibleOffererUserIds { get; set; }

    public Crew Crew { get; set; } = null!;
    public User RequesterUser { get; set; } = null!;
    public ICollection<EmergencySplitOffer> SplitOffers { get; set; } = new List<EmergencySplitOffer>();
    public ICollection<EmergencyGiftResponse> GiftResponses { get; set; } = new List<EmergencyGiftResponse>();
}
