namespace LiberationFleet.Server.Application.Features.Rules;

public static class CrewRuleChangeDescriber
{
    public const string CreateTitle = "New rule";
    public const string UpdateTitle = "Editing rule";
    public const string DeleteTitle = "Removing rule";

    public static string FormatRuleText(string title, string description)
    {
        var trimmedTitle = title.Trim();
        var trimmedDescription = description.Trim();
        return string.IsNullOrWhiteSpace(trimmedDescription)
            ? trimmedTitle
            : $"{trimmedTitle}: {trimmedDescription}";
    }

    public static string BuildCreateDescription(string title, string description) =>
        $"Proposal to add this new rule \"{FormatRuleText(title, description)}\".";

    public static string BuildUpdateDescription(
        string oldTitle,
        string oldDescription,
        string newTitle,
        string newDescription) =>
        $"Proposal to change the following rule from \"{FormatRuleText(oldTitle, oldDescription)}\" to \"{FormatRuleText(newTitle, newDescription)}\".";

    public static string BuildDeleteDescription(string title, string description) =>
        $"Proposal to delete the following rule \"{FormatRuleText(title, description)}\".";
}
