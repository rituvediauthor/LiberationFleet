using LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleet;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Fleets;

public sealed record FleetSettingChangeItem(
    FleetSettingField Field,
    string? OldValue,
    string NewValue);

public static class FleetSettingsChangeDetector
{
    public static IReadOnlyList<FleetSettingChangeItem> DetectChanges(
        Fleet fleet,
        UpdateFleetCommand request,
        CrewPrivacy privacy,
        CrewScope scope)
    {
        var changes = new List<FleetSettingChangeItem>();
        var trimmedName = request.Name.Trim();

        if (!string.Equals(fleet.Name, trimmedName, StringComparison.Ordinal))
        {
            changes.Add(new FleetSettingChangeItem(FleetSettingField.Name, fleet.Name, trimmedName));
        }

        if (fleet.Privacy != privacy)
        {
            changes.Add(new FleetSettingChangeItem(FleetSettingField.Privacy, fleet.Privacy.ToString(), privacy.ToString()));
        }

        if (fleet.Scope != scope)
        {
            changes.Add(new FleetSettingChangeItem(FleetSettingField.Scope, fleet.Scope.ToString(), scope.ToString()));
        }

        var zip = scope == CrewScope.Local ? request.ZipCode?.Trim() : null;
        if (!string.Equals(fleet.ZipCode, zip, StringComparison.Ordinal))
        {
            changes.Add(new FleetSettingChangeItem(FleetSettingField.ZipCode, fleet.ZipCode ?? string.Empty, zip ?? string.Empty));
        }

        var radius = scope == CrewScope.Local ? request.RadiusMiles : null;
        if (fleet.RadiusMiles != radius)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.RadiusMiles,
                fleet.RadiusMiles?.ToString() ?? string.Empty,
                radius?.ToString() ?? string.Empty));
        }

        if (fleet.RequireApprovalForEdits != request.RequireApprovalForEdits)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.RequireApprovalForEdits,
                fleet.RequireApprovalForEdits.ToString(),
                request.RequireApprovalForEdits.ToString()));
        }

        if (fleet.LibraryOfThingsEnabled != request.LibraryOfThingsEnabled)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.LibraryOfThingsEnabled,
                fleet.LibraryOfThingsEnabled.ToString(),
                request.LibraryOfThingsEnabled.ToString()));
        }

        if (fleet.AllowCrewmateFileAttachments != request.AllowCrewmateFileAttachments)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.AllowCrewmateFileAttachments,
                fleet.AllowCrewmateFileAttachments.ToString(),
                request.AllowCrewmateFileAttachments.ToString()));
        }

        if (fleet.MinimumCrewmateTenureDaysForAttachments != request.MinimumCrewmateTenureDaysForAttachments)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.MinimumCrewmateTenureDaysForAttachments,
                fleet.MinimumCrewmateTenureDaysForAttachments.ToString(),
                request.MinimumCrewmateTenureDaysForAttachments.ToString()));
        }

        if (fleet.MinimumContributionForAttachments != request.MinimumContributionForAttachments)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.MinimumContributionForAttachments,
                fleet.MinimumContributionForAttachments.ToString("0.##"),
                request.MinimumContributionForAttachments.ToString("0.##")));
        }

        if (fleet.MinimumCrewmateTenureDaysForProposals != request.MinimumCrewmateTenureDaysForProposals)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.MinimumCrewmateTenureDaysForProposals,
                fleet.MinimumCrewmateTenureDaysForProposals.ToString(),
                request.MinimumCrewmateTenureDaysForProposals.ToString()));
        }

        if (fleet.MinimumContributionForProposals != request.MinimumContributionForProposals)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.MinimumContributionForProposals,
                fleet.MinimumContributionForProposals.ToString("0.##"),
                request.MinimumContributionForProposals.ToString("0.##")));
        }

        return changes;
    }
}

public static class FleetSettingsChangeDescriber
{
    public const string DefaultTitle = "Editing fleet settings";

    public static string BuildDescription(FleetSettingChangeItem change) =>
        change.Field switch
        {
            FleetSettingField.Name =>
                $"Proposal to change the fleet name from \"{change.OldValue}\" to \"{change.NewValue}\".",
            FleetSettingField.Privacy =>
                $"Proposal to change fleet privacy from {change.OldValue} to {change.NewValue}.",
            FleetSettingField.Scope =>
                $"Proposal to change fleet location type from {change.OldValue} to {change.NewValue}.",
            FleetSettingField.ZipCode =>
                $"Proposal to change zip code from \"{change.OldValue}\" to \"{change.NewValue}\".",
            FleetSettingField.RadiusMiles =>
                $"Proposal to change distance from {change.OldValue} to {change.NewValue} miles.",
            FleetSettingField.RequireApprovalForEdits =>
                $"Proposal to set \"Require approval for fleet edits\" to \"{change.NewValue}\".",
            FleetSettingField.LibraryOfThingsEnabled =>
                $"Proposal to set \"Library of Things\" to \"{change.NewValue}\".",
            FleetSettingField.AllowCrewmateFileAttachments =>
                $"Proposal to set \"Allow crewmate file attachments\" to \"{change.NewValue}\".",
            FleetSettingField.MinimumCrewmateTenureDaysForAttachments =>
                $"Proposal to change minimum tenure for attachments from {change.OldValue} to {change.NewValue} days.",
            FleetSettingField.MinimumContributionForAttachments =>
                $"Proposal to change minimum contribution for attachments from ${change.OldValue} to ${change.NewValue}.",
            FleetSettingField.MinimumCrewmateTenureDaysForProposals =>
                $"Proposal to change minimum tenure for proposals from {change.OldValue} to {change.NewValue} days.",
            FleetSettingField.MinimumContributionForProposals =>
                $"Proposal to change minimum contribution for proposals from ${change.OldValue} to ${change.NewValue}.",
            _ => "Proposal to change fleet settings."
        };
}
