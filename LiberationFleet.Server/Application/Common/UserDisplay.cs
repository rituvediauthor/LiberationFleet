using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

/// <summary>
/// Public display labels for users. Placeholders may use <see cref="User.DisplayName"/>
/// while keeping a synthetic unique <see cref="User.Username"/>.
/// </summary>
public static class UserDisplay
{
    public static string Name(User? user) =>
        user is null
            ? string.Empty
            : !string.IsNullOrWhiteSpace(user.DisplayName)
                ? user.DisplayName.Trim()
                : user.Username;
}
