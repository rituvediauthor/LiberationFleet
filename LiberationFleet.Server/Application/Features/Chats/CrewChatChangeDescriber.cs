namespace LiberationFleet.Server.Application.Features.Chats;

public static class CrewChatChangeDescriber
{
    public const string CreateTitle = "Create chat channel";
    public const string UpdateTitle = "Rename / update chat channel";
    public const string DeleteTitle = "Delete chat channel";

    public static string BuildCreateDescription(string name, string purpose) =>
        $"Create a new chat channel named \"{name.Trim()}\".\nPurpose: \"{purpose.Trim()}\".";

    public static string BuildUpdateDescription(
        string oldName,
        string oldPurpose,
        string newName,
        string newPurpose)
    {
        var nameChanged = !string.Equals(oldName.Trim(), newName.Trim(), StringComparison.Ordinal);
        var purposeChanged = !string.Equals(oldPurpose.Trim(), newPurpose.Trim(), StringComparison.Ordinal);
        if (nameChanged && purposeChanged)
        {
            return
                $"Update chat channel:\n" +
                $"• Name: \"{oldName.Trim()}\" → \"{newName.Trim()}\"\n" +
                $"• Purpose: \"{oldPurpose.Trim()}\" → \"{newPurpose.Trim()}\"";
        }

        if (nameChanged)
        {
            return $"Rename chat channel from \"{oldName.Trim()}\" to \"{newName.Trim()}\".";
        }

        if (purposeChanged)
        {
            return
                $"Update purpose of chat channel \"{newName.Trim()}\":\n" +
                $"\"{oldPurpose.Trim()}\" → \"{newPurpose.Trim()}\"";
        }

        return $"Update chat channel \"{newName.Trim()}\" (no visible name/purpose change).";
    }

    public static string BuildDeleteDescription(string name, string purpose) =>
        $"Delete chat channel \"{name.Trim()}\".\nPurpose was: \"{purpose.Trim()}\".";
}
