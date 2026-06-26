using LiberationFleet.Server.Application.Features.Crews.Commands.UpdateCrew;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed record CrewSettingChangeItem(
    CrewSettingField Field,
    string? OldValue,
    string NewValue);

public static class CrewSettingsChangeDetector
{
    public static IReadOnlyList<CrewSettingChangeItem> DetectChanges(
        Crew crew,
        UpdateCrewCommand request,
        CrewPrivacy privacy,
        CrewScope scope)
    {
        var changes = new List<CrewSettingChangeItem>();
        var trimmedName = request.Name.Trim();

        if (!string.Equals(crew.Name, trimmedName, StringComparison.Ordinal))
        {
            changes.Add(new CrewSettingChangeItem(CrewSettingField.Name, crew.Name, trimmedName));
        }

        if (crew.MaxSize != request.MaxSize)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MaxSize,
                crew.MaxSize.ToString(),
                request.MaxSize.ToString()));
        }

        if (crew.Privacy != privacy)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.Privacy,
                crew.Privacy.ToString(),
                privacy.ToString()));
        }

        if (crew.Scope != scope)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.Scope,
                crew.Scope.ToString(),
                scope.ToString()));
        }

        if (scope == CrewScope.Local)
        {
            var newZip = request.ZipCode?.Trim();
            var currentZip = crew.ZipCode;
            if (!string.Equals(currentZip, newZip, StringComparison.Ordinal))
            {
                changes.Add(new CrewSettingChangeItem(
                    CrewSettingField.ZipCode,
                    currentZip ?? string.Empty,
                    newZip ?? string.Empty));
            }

            var newRadius = request.RadiusMiles;
            if (crew.RadiusMiles != newRadius)
            {
                changes.Add(new CrewSettingChangeItem(
                    CrewSettingField.RadiusMiles,
                    crew.RadiusMiles?.ToString() ?? string.Empty,
                    newRadius?.ToString() ?? string.Empty));
            }
        }

        if (crew.AllowSurvivalThresholds != request.AllowSurvivalThresholds)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.AllowSurvivalThresholds,
                crew.AllowSurvivalThresholds.ToString(),
                request.AllowSurvivalThresholds.ToString()));
        }

        if (crew.RequireApprovalForEdits != request.RequireApprovalForEdits)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.RequireApprovalForEdits,
                crew.RequireApprovalForEdits.ToString(),
                request.RequireApprovalForEdits.ToString()));
        }

        if (crew.InNeedDefaultThreshold != request.InNeedDefaultThreshold)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.InNeedDefaultThreshold,
                crew.InNeedDefaultThreshold.ToString("0.##"),
                request.InNeedDefaultThreshold.ToString("0.##")));
        }

        return changes;
    }
}

public static class CrewSettingsChangeDescriber
{
    public const string DefaultTitle = "Editing crew settings";

    public static string BuildDescription(CrewSettingChangeItem change) =>
        change.Field switch
        {
            CrewSettingField.Name =>
                $"Proposal to change the crew name from \"{change.OldValue}\" to \"{change.NewValue}\".",
            CrewSettingField.MaxSize =>
                $"Proposal to change crew size from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.Privacy =>
                $"Proposal to change crew privacy from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.Scope =>
                $"Proposal to change crew location type from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.ZipCode =>
                $"Proposal to change zip code from \"{change.OldValue}\" to \"{change.NewValue}\".",
            CrewSettingField.RadiusMiles =>
                $"Proposal to change distance from {change.OldValue} to {change.NewValue} miles.",
            CrewSettingField.AllowSurvivalThresholds =>
                $"Proposal to set \"Allow survival thresholds\" to \"{FormatBool(change.NewValue)}\".",
            CrewSettingField.RequireApprovalForEdits =>
                $"Proposal to set \"Require approval for crew edits\" to \"{FormatBool(change.NewValue)}\".",
            CrewSettingField.InNeedDefaultThreshold =>
                $"Proposal to change in-need default threshold from ${change.OldValue} to ${change.NewValue}.",
            _ => "Proposal to change crew settings."
        };

    private static string FormatBool(string value) =>
        bool.TryParse(value, out var parsed) && parsed ? "True" : "False";
}
