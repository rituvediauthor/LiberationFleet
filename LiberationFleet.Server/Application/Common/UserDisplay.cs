using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

/// <summary>
/// Public display labels for users. API "Username" fields that are shown in UI should use
/// <see cref="Name"/>. Real account users keep <see cref="User.DisplayName"/> equal to
/// <see cref="User.Username"/>; unclaimed placeholders keep a human <see cref="User.DisplayName"/>
/// with a synthetic unique <see cref="User.Username"/>.
/// </summary>
public static class UserDisplay
{
    public static string Name(User? user) =>
        user is null ? string.Empty : Name(user.DisplayName, user.Username);

    public static string Name(string? displayName, string? username) =>
        !string.IsNullOrWhiteSpace(displayName)
            ? displayName.Trim()
            : username?.Trim() ?? string.Empty;

    /// <summary>
    /// For normal (non-placeholder) accounts, keep DisplayName aligned with Username so every
    /// display surface can call <see cref="Name"/> safely.
    /// </summary>
    public static void SyncAccountDisplayName(User user)
    {
        if (user.IsUnclaimedPlaceholder || user.IsCrewGiftRecipient)
        {
            return;
        }

        user.DisplayName = user.Username?.Trim();
    }
}
