using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Rules;

public static class CrewRuleChangeDescriber
{
    public const string CreateTitle = "Add crew rule";
    public const string UpdateTitle = "Edit crew rule";
    public const string DeleteTitle = "Delete crew rule";

    public static string FormatRuleText(string title, string description)
    {
        var trimmedTitle = title.Trim();
        var trimmedDescription = description.Trim();
        return string.IsNullOrWhiteSpace(trimmedDescription)
            ? trimmedTitle
            : $"{trimmedTitle}: {trimmedDescription}";
    }

    /// <summary>
    /// Proposal rows store the rule body in Title/Description (apply needs them).
    /// Map those into a clearer list/detail presentation.
    /// </summary>
    public static (string Title, string Description) BuildProposalDisplay(
        CrewRuleProposalAction action,
        string ruleTitle,
        string ruleDescription)
    {
        var body = FormatRuleText(ruleTitle, ruleDescription);
        return action switch
        {
            CrewRuleProposalAction.Create => (
                CreateTitle,
                string.IsNullOrWhiteSpace(body)
                    ? "Add a new crew rule (content encrypted)."
                    : $"Add this crew rule:\n\"{body}\""),
            CrewRuleProposalAction.Update => (
                UpdateTitle,
                string.IsNullOrWhiteSpace(body)
                    ? "Edit an existing crew rule (content encrypted)."
                    : $"Update the crew rule to:\n\"{body}\""),
            CrewRuleProposalAction.Delete => (
                DeleteTitle,
                string.IsNullOrWhiteSpace(body)
                    ? "Delete a crew rule (content encrypted)."
                    : $"Delete this crew rule:\n\"{body}\""),
            _ => ("Crew rule change", body)
        };
    }

    public static string BuildCreateDescription(string title, string description) =>
        $"Add this crew rule:\n\"{FormatRuleText(title, description)}\"";

    public static string BuildUpdateDescription(
        string oldTitle,
        string oldDescription,
        string newTitle,
        string newDescription) =>
        $"Edit crew rule:\n" +
        $"From: \"{FormatRuleText(oldTitle, oldDescription)}\"\n" +
        $"To: \"{FormatRuleText(newTitle, newDescription)}\"";

    public static string BuildDeleteDescription(string title, string description) =>
        $"Delete this crew rule:\n\"{FormatRuleText(title, description)}\"";
}
