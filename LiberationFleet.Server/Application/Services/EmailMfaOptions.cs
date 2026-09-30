namespace LiberationFleet.Server.Application.Services;

public class EmailMfaOptions
{
    public const string SectionName = "EmailMfa";

    public int CodeTtlMinutes { get; set; } = 10;
    public int MaxAttempts { get; set; } = 5;
    public int ResendCooldownSeconds { get; set; } = 60;
}
