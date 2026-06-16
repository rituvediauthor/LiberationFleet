namespace LiberationFleet.Server.Application.Features.Profile.Contracts;

public class UserGiftStats
{
    public decimal LifetimeContributions { get; set; }
    public int SacrificeCountLastYear { get; set; }
    public decimal ContributionsLast3Months { get; set; }
    public decimal ReceptionLastYear { get; set; }
}
