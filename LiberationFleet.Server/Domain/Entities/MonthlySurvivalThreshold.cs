namespace LiberationFleet.Server.Domain.Entities;

public class MonthlySurvivalThreshold
{
    public int Id { get; set; }
    public int CrewId { get; set; }
    public int UserId { get; set; }
    public int Year { get; set; }
    public int Month { get; set; }
    public decimal ThresholdAmount { get; set; }
    public decimal ReceivedAmount { get; set; }
    public int ReceptionOrderPosition { get; set; }
    public bool Satisfied { get; set; }
    /// <summary>
    /// When true, monthly auto-refresh must not overwrite <see cref="ThresholdAmount"/>
    /// (set by aid-stat drafts / accountant edits).
    /// </summary>
    public bool IsCustomAmount { get; set; }

    public Crew Crew { get; set; } = null!;
    public User User { get; set; } = null!;
}
