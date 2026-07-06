namespace LiberationFleet.Server.Application.Features.Crews;

public static class PlaceholderUserDefaults
{
    public const string PasswordHash = "UNCLAIMED_PLACEHOLDER_NO_LOGIN";

    public static string CreateInternalEmail() =>
        $"placeholder+{Guid.NewGuid():N}@placeholder.liberationfleet.invalid";
}
