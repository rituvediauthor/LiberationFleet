namespace LiberationFleet.Server.Domain.Entities;

public class FleetAllowedZipCode
{
    public int FleetId { get; set; }
    public string ZipCode { get; set; } = string.Empty;

    public Fleet Fleet { get; set; } = null!;
}
