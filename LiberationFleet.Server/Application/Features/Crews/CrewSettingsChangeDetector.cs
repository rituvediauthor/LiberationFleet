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

        if (crew.LibraryOfThingsEnabled != request.LibraryOfThingsEnabled)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.LibraryOfThingsEnabled,
                crew.LibraryOfThingsEnabled.ToString(),
                request.LibraryOfThingsEnabled.ToString()));
        }

        var memberCycleCapMode = CrewUpdateValidator.ParseCycleCapMode(request.MemberCycleCapMode);
        if (crew.MemberCycleCapMode != memberCycleCapMode)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MemberCycleCapMode,
                crew.MemberCycleCapMode.ToString(),
                memberCycleCapMode.ToString()));
        }

        if (crew.MemberCycleCapFixedAmount != request.MemberCycleCapFixedAmount)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MemberCycleCapFixedAmount,
                crew.MemberCycleCapFixedAmount.ToString("0.##"),
                request.MemberCycleCapFixedAmount.ToString("0.##")));
        }

        if (crew.MemberCycleCapMultiplier != request.MemberCycleCapMultiplier)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MemberCycleCapMultiplier,
                crew.MemberCycleCapMultiplier.ToString("0.####"),
                request.MemberCycleCapMultiplier.ToString("0.####")));
        }

        var nonMemberCycleCapMode = CrewUpdateValidator.ParseCycleCapMode(request.NonMemberCycleCapMode);
        if (crew.NonMemberCycleCapMode != nonMemberCycleCapMode)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.NonMemberCycleCapMode,
                crew.NonMemberCycleCapMode.ToString(),
                nonMemberCycleCapMode.ToString()));
        }

        if (crew.NonMemberCycleCapFixedAmount != request.NonMemberCycleCapFixedAmount)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.NonMemberCycleCapFixedAmount,
                crew.NonMemberCycleCapFixedAmount.ToString("0.##"),
                request.NonMemberCycleCapFixedAmount.ToString("0.##")));
        }

        if (crew.NonMemberCycleCapMultiplier != request.NonMemberCycleCapMultiplier)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.NonMemberCycleCapMultiplier,
                crew.NonMemberCycleCapMultiplier.ToString("0.####"),
                request.NonMemberCycleCapMultiplier.ToString("0.####")));
        }

        if (crew.AllowCrewmateFileAttachments != request.AllowCrewmateFileAttachments)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.AllowCrewmateFileAttachments,
                crew.AllowCrewmateFileAttachments.ToString(),
                request.AllowCrewmateFileAttachments.ToString()));
        }

        if (crew.MinimumCrewmateTenureDaysForAttachments != request.MinimumCrewmateTenureDaysForAttachments)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MinimumCrewmateTenureDaysForAttachments,
                crew.MinimumCrewmateTenureDaysForAttachments.ToString(),
                request.MinimumCrewmateTenureDaysForAttachments.ToString()));
        }

        if (crew.MinimumContributionForAttachments != request.MinimumContributionForAttachments)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MinimumContributionForAttachments,
                crew.MinimumContributionForAttachments.ToString("0.##"),
                request.MinimumContributionForAttachments.ToString("0.##")));
        }

        if (crew.MinimumCrewmateTenureDaysForProposals != request.MinimumCrewmateTenureDaysForProposals)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MinimumCrewmateTenureDaysForProposals,
                crew.MinimumCrewmateTenureDaysForProposals.ToString(),
                request.MinimumCrewmateTenureDaysForProposals.ToString()));
        }

        if (crew.MinimumContributionForProposals != request.MinimumContributionForProposals)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.MinimumContributionForProposals,
                crew.MinimumContributionForProposals.ToString("0.##"),
                request.MinimumContributionForProposals.ToString("0.##")));
        }

        if (crew.AllowCrossCrewGiving != request.AllowCrossCrewGiving)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.AllowCrossCrewGiving,
                crew.AllowCrossCrewGiving.ToString(),
                request.AllowCrossCrewGiving.ToString()));
        }

        var newImageResourceId = NormalizeResourceId(request.ImageResourceId);
        var currentImageResourceId = crew.ImageResourceId ?? string.Empty;
        if (!string.Equals(currentImageResourceId, newImageResourceId, StringComparison.Ordinal))
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.ImageResourceId,
                currentImageResourceId,
                newImageResourceId));
        }

        return changes;
    }

    public static string NormalizeResourceId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
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
            CrewSettingField.LibraryOfThingsEnabled =>
                $"Proposal to set \"Library of Things\" to \"{FormatBool(change.NewValue)}\".",
            CrewSettingField.MemberCycleCapMode =>
                $"Proposal to change member cycle cap mode from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.MemberCycleCapFixedAmount =>
                $"Proposal to change member fixed cycle cap from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.MemberCycleCapMultiplier =>
                $"Proposal to change member cycle cap multiplier from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.NonMemberCycleCapMode =>
                $"Proposal to change non-member cycle cap mode from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.NonMemberCycleCapFixedAmount =>
                $"Proposal to change non-member fixed cycle cap from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.NonMemberCycleCapMultiplier =>
                $"Proposal to change non-member cycle cap multiplier from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.AllowCrewmateFileAttachments =>
                $"Proposal to set \"Allow crewmate file attachments\" to \"{FormatBool(change.NewValue)}\".",
            CrewSettingField.MinimumCrewmateTenureDaysForAttachments =>
                $"Proposal to change minimum crewmate tenure for attachments from {change.OldValue} to {change.NewValue} days.",
            CrewSettingField.MinimumContributionForAttachments =>
                $"Proposal to change minimum contribution for attachments from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.MinimumCrewmateTenureDaysForProposals =>
                $"Proposal to change minimum crewmate tenure for proposals from {change.OldValue} to {change.NewValue} days.",
            CrewSettingField.MinimumContributionForProposals =>
                $"Proposal to change minimum contribution for proposals from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.AllowCrossCrewGiving =>
                $"Proposal to set \"Allow cross-crew giving\" to \"{FormatBool(change.NewValue)}\".",
            CrewSettingField.ImageResourceId =>
                string.IsNullOrEmpty(change.NewValue)
                    ? "Proposal to remove the crew image."
                    : "Proposal to set a new crew image.",
            _ => "Proposal to change crew settings."
        };

    private static string FormatBool(string value) =>
        bool.TryParse(value, out var parsed) && parsed ? "True" : "False";
}
