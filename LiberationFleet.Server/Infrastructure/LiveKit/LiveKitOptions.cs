namespace LiberationFleet.Server.Infrastructure.LiveKit;

public class LiveKitOptions
{
    public const string SectionName = "LiveKit";

    public string Host { get; set; } = "ws://localhost:7880";
    public string ApiKey { get; set; } = string.Empty;
    public string ApiSecret { get; set; } = string.Empty;
    public int TokenTtlMinutes { get; set; } = 360;
}
