namespace LiberationFleet.Server.Application.Features.Crews;

public static class PlaceholderUserDefaults
{
    public const string PasswordHash = "UNCLAIMED_PLACEHOLDER_NO_LOGIN";

    public static string CreateInternalEmail() =>
        $"placeholder+{Guid.NewGuid():N}@placeholder.liberationfleet.invalid";

    /// <summary>Synthetic unique login username that never collides with real accounts' chosen names.</summary>
    public static string CreateSyntheticUsername() =>
        $"ph_{Guid.NewGuid():N}";
}
