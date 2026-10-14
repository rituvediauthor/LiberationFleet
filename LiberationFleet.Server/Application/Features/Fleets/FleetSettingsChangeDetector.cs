using LiberationFleet.Server.Application.Common;
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

        var requestedCountry = scope == CrewScope.Local
            ? CountryCodes.Normalize(request.CountryCode) ?? string.Empty
            : string.Empty;
        var currentCountry = fleet.CountryCode ?? string.Empty;
        if (!string.Equals(currentCountry, requestedCountry, StringComparison.Ordinal))
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.CountryCode,
                currentCountry,
                requestedCountry));
        }

        var requestedZips = scope == CrewScope.Local
            ? ZipCodeList.Normalize(request.AllowedZipCodes)
            : Array.Empty<string>();
        var currentZips = AllowedZipCodeSync.GetFleetZips(fleet);
        if (!ZipCodeList.SequenceEqual(currentZips, requestedZips))
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.AllowedZipCodes,
                ZipCodeList.Serialize(currentZips),
                ZipCodeList.Serialize(requestedZips)));
        }

        if (fleet.RequireApprovalForEdits != request.RequireApprovalForEdits)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.RequireApprovalForEdits,
                fleet.RequireApprovalForEdits.ToString(),
                request.RequireApprovalForEdits.ToString()));
        }

        var duoMode = Enum.TryParse<DuoVoteTimeoutMode>(request.DuoVoteTimeoutMode, true, out var parsedDuo)
            && Enum.IsDefined(parsedDuo)
            ? parsedDuo
            : DuoVoteTimeoutMode.AutoReject;
        if (fleet.DuoVoteTimeoutMode != duoMode)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.DuoVoteTimeoutMode,
                fleet.DuoVoteTimeoutMode.ToString(),
                duoMode.ToString()));
        }

        if (fleet.AutoResolveOverTime != request.AutoResolveOverTime)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.AutoResolveOverTime,
                fleet.AutoResolveOverTime.ToString(),
                request.AutoResolveOverTime.ToString()));
        }

        if (fleet.BaseAutoResolveHours != request.BaseAutoResolveHours)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.BaseAutoResolveHours,
                fleet.BaseAutoResolveHours.ToString(),
                request.BaseAutoResolveHours.ToString()));
        }

        if (fleet.ChangeAutoResolveTimerOnFirstReject != request.ChangeAutoResolveTimerOnFirstReject)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.ChangeAutoResolveTimerOnFirstReject,
                fleet.ChangeAutoResolveTimerOnFirstReject.ToString(),
                request.ChangeAutoResolveTimerOnFirstReject.ToString()));
        }

        if (fleet.AutoResolveHoursAfterFirstReject != request.AutoResolveHoursAfterFirstReject)
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.AutoResolveHoursAfterFirstReject,
                fleet.AutoResolveHoursAfterFirstReject.ToString(),
                request.AutoResolveHoursAfterFirstReject.ToString()));
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

        var newImageResourceId = NormalizeResourceId(request.ImageResourceId);
        var currentImageResourceId = fleet.ImageResourceId ?? string.Empty;
        if (!string.Equals(currentImageResourceId, newImageResourceId, StringComparison.Ordinal))
        {
            changes.Add(new FleetSettingChangeItem(
                FleetSettingField.ImageResourceId,
                currentImageResourceId,
                newImageResourceId));
        }

        return changes;
    }

    public static string NormalizeResourceId(string? value) =>
        string.IsNullOrWhiteSpace(value) ? string.Empty : value.Trim();
}

public static class FleetSettingsChangeDescriber
{
    public const string DefaultTitle = "Change fleet setting";

    public static string BuildTitle(FleetSettingChangeItem change) =>
        change.Field switch
        {
            FleetSettingField.Name => "Change fleet name",
            FleetSettingField.Privacy => "Change fleet privacy",
            FleetSettingField.Scope => "Change fleet location type",
            FleetSettingField.CountryCode => "Change fleet country",
            FleetSettingField.AllowedZipCodes => "Change allowed postal codes",
            FleetSettingField.RadiusMiles => "Change distance setting (legacy)",
            FleetSettingField.RequireApprovalForEdits => "Change edit-approval requirement",
            FleetSettingField.DuoVoteTimeoutMode => "Change tied-vote timeout",
            FleetSettingField.AutoResolveOverTime => "Change auto-resolve over time",
            FleetSettingField.BaseAutoResolveHours => "Change initial auto-resolve window",
            FleetSettingField.ChangeAutoResolveTimerOnFirstReject => "Change timer-on-first-reject setting",
            FleetSettingField.AutoResolveHoursAfterFirstReject => "Change post-reject auto-resolve window",
            FleetSettingField.LibraryOfThingsEnabled => "Change Library of Things setting",
            FleetSettingField.AllowCrewmateFileAttachments => "Change file-attachment permission",
            FleetSettingField.MinimumCrewmateTenureDaysForAttachments => "Change attachment tenure requirement",
            FleetSettingField.MinimumContributionForAttachments => "Change attachment contribution requirement",
            FleetSettingField.MinimumCrewmateTenureDaysForProposals => "Change proposal tenure requirement",
            FleetSettingField.MinimumContributionForProposals => "Change proposal contribution requirement",
            FleetSettingField.ImageResourceId => string.IsNullOrEmpty(change.NewValue)
                ? "Remove fleet image"
                : "Change fleet image",
            _ => DefaultTitle
        };

    public static string BuildDescription(FleetSettingChangeItem change) =>
        change.Field switch
        {
            FleetSettingField.Name =>
                $"Change the fleet name from \"{change.OldValue}\" to \"{change.NewValue}\".",
            FleetSettingField.Privacy =>
                $"Change fleet privacy from {change.OldValue} to {change.NewValue}.",
            FleetSettingField.Scope =>
                $"Change fleet location type from {change.OldValue} to {change.NewValue}.",
            FleetSettingField.CountryCode =>
                $"Change country from \"{FormatCountry(change.OldValue)}\" to \"{FormatCountry(change.NewValue)}\".",
            FleetSettingField.AllowedZipCodes =>
                $"Change allowed postal codes from \"{ZipCodeList.FormatDisplay(ZipCodeList.Deserialize(change.OldValue))}\" to \"{ZipCodeList.FormatDisplay(ZipCodeList.Deserialize(change.NewValue))}\".",
            FleetSettingField.RadiusMiles =>
                $"Change distance from {change.OldValue} to {change.NewValue} miles (legacy; no longer applied).",
            FleetSettingField.RequireApprovalForEdits =>
                $"Turn \"Require approval for fleet edits\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            FleetSettingField.DuoVoteTimeoutMode =>
                $"Change tied-vote timeout (equal approve/reject when the timer expires) from \"{FormatDuoMode(change.OldValue)}\" to \"{FormatDuoMode(change.NewValue)}\".",
            FleetSettingField.AutoResolveOverTime =>
                $"Turn \"Auto-resolve over time\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}. " +
                "When enabled, the side with more votes wins at timer expiry; equal counts use tied-vote timeout.",
            FleetSettingField.BaseAutoResolveHours =>
                $"Change the initial auto-resolve window from {change.OldValue} to {change.NewValue} hours.",
            FleetSettingField.ChangeAutoResolveTimerOnFirstReject =>
                $"Turn \"Change auto-resolve timer on first reject\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            FleetSettingField.AutoResolveHoursAfterFirstReject =>
                $"Change the post-reject auto-resolve window from {change.OldValue} to {change.NewValue} hours.",
            FleetSettingField.LibraryOfThingsEnabled =>
                $"Turn \"Library of Things\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            FleetSettingField.AllowCrewmateFileAttachments =>
                $"Turn \"Allow crewmate file attachments\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            FleetSettingField.MinimumCrewmateTenureDaysForAttachments =>
                $"Change minimum tenure for attachments from {change.OldValue} to {change.NewValue} days.",
            FleetSettingField.MinimumContributionForAttachments =>
                $"Change minimum contribution for attachments from ${change.OldValue} to ${change.NewValue}.",
            FleetSettingField.MinimumCrewmateTenureDaysForProposals =>
                $"Change minimum tenure for proposals from {change.OldValue} to {change.NewValue} days.",
            FleetSettingField.MinimumContributionForProposals =>
                $"Change minimum contribution for proposals from ${change.OldValue} to ${change.NewValue}.",
            FleetSettingField.ImageResourceId =>
                string.IsNullOrEmpty(change.NewValue)
                    ? "Remove the fleet image."
                    : "Replace the fleet image with a new one.",
            _ => "Change a fleet setting."
        };

    private static string FormatBool(string? value) =>
        bool.TryParse(value, out var parsed) && parsed ? "Yes" : "No";

    private static string FormatCountry(string? value) =>
        string.IsNullOrWhiteSpace(value) ? "(none)" : value.Trim().ToUpperInvariant();

    private static string FormatDuoMode(string? value) =>
        value switch
        {
            nameof(DuoVoteTimeoutMode.AutoApprove) => "Auto approve",
            nameof(DuoVoteTimeoutMode.ResolveOnFirstVote) => "Resolve on next vote",
            _ => "Auto reject"
        };
}
