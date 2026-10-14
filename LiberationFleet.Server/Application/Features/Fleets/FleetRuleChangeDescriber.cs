using LiberationFleet.Server.Application.Features.Rules;

namespace LiberationFleet.Server.Application.Features.Fleets;

public static class FleetRuleChangeDescriber
{
    public const string CreateTitle = "Add fleet rule";
    public const string UpdateTitle = "Edit fleet rule";
    public const string DeleteTitle = "Delete fleet rule";

    public static string BuildCreateDescription(string title, string description) =>
        $"Add this fleet rule:\n\"{CrewRuleChangeDescriber.FormatRuleText(title, description)}\"";

    public static string BuildUpdateDescription(
        string oldTitle,
        string oldDescription,
        string newTitle,
        string newDescription) =>
        $"Edit fleet rule:\n" +
        $"From: \"{CrewRuleChangeDescriber.FormatRuleText(oldTitle, oldDescription)}\"\n" +
        $"To: \"{CrewRuleChangeDescriber.FormatRuleText(newTitle, newDescription)}\"";

    public static string BuildDeleteDescription(string title, string description) =>
        $"Delete this fleet rule:\n\"{CrewRuleChangeDescriber.FormatRuleText(title, description)}\"";
}
