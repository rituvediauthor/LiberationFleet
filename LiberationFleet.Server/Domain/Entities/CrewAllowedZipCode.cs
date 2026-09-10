namespace LiberationFleet.Server.Domain.Entities;

public class CrewAllowedZipCode
{
    public int CrewId { get; set; }
    public string ZipCode { get; set; } = string.Empty;

    public Crew Crew { get; set; } = null!;
}
