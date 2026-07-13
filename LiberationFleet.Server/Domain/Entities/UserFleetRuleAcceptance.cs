namespace LiberationFleet.Server.Domain.Entities;

public class UserFleetRuleAcceptance
{
    public int Id { get; set; }
    public int UserId { get; set; }
    public int FleetId { get; set; }
    public string AcceptedRuleIdsJson { get; set; } = "[]";
    public DateTime AcceptedAt { get; set; } = DateTime.UtcNow;

    public User User { get; set; } = null!;
    public Fleet Fleet { get; set; } = null!;
}
