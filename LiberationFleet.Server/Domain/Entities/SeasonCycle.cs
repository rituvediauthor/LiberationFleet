namespace LiberationFleet.Server.Domain.Entities;

public class SeasonCycle
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int UserId { get; set; }
    public DateTime SeasonStartDate { get; set; }
    public decimal CycleCapAtStart { get; set; }
    public decimal TotalReceptionAmount { get; set; }
    public decimal SurvivalThresholdReceived { get; set; }
    public decimal CycleReceived { get; set; }
    public bool CycleCompleted { get; set; }
    public DateTime? CycleCompletedAt { get; set; }
    public decimal PriorityScoreAtSeasonStart { get; set; }
    public int ReceptionOrderPosition { get; set; }

    public Crew Crew { get; set; } = null!;
    public User User { get; set; } = null!;
}
