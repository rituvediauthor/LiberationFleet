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

    public Crew Crew { get; set; } = null!;
    public User User { get; set; } = null!;
}
