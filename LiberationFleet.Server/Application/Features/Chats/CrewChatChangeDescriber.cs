namespace LiberationFleet.Server.Application.Features.Chats;

public static class CrewChatChangeDescriber
{
    public const string CreateTitle = "New chat channel";
    public const string UpdateTitle = "Editing chat channel";
    public const string DeleteTitle = "Removing chat channel";

    public static string BuildCreateDescription(string name, string purpose) =>
        $"Proposal to create chat channel \"{name.Trim()}\" with purpose \"{purpose.Trim()}\".";

    public static string BuildUpdateDescription(
        string oldName,
        string oldPurpose,
        string newName,
        string newPurpose) =>
        $"Proposal to change the chat channel from \"{oldName.Trim()}\" to \"{newName.Trim()}\" and purpose from \"{oldPurpose.Trim()}\" to \"{newPurpose.Trim()}\".";

    public static string BuildDeleteDescription(string name, string purpose) =>
        $"Proposal to delete the chat channel \"{name.Trim()}\" with purpose \"{purpose.Trim()}\".";
}
