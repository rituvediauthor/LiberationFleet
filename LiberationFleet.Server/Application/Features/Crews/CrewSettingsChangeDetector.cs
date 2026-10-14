using LiberationFleet.Server.Application.Common;
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

        var requestedCountry = scope == CrewScope.Local
            ? CountryCodes.Normalize(request.CountryCode) ?? string.Empty
            : string.Empty;
        var currentCountry = crew.CountryCode ?? string.Empty;
        if (!string.Equals(currentCountry, requestedCountry, StringComparison.Ordinal))
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.CountryCode,
                currentCountry,
                requestedCountry));
        }

        var requestedZips = scope == CrewScope.Local
            ? ZipCodeList.Normalize(request.AllowedZipCodes)
            : Array.Empty<string>();
        var currentZips = AllowedZipCodeSync.GetCrewZips(crew);
        if (!ZipCodeList.SequenceEqual(currentZips, requestedZips))
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.AllowedZipCodes,
                ZipCodeList.Serialize(currentZips),
                ZipCodeList.Serialize(requestedZips)));
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

        var duoMode = Enum.TryParse<DuoVoteTimeoutMode>(request.DuoVoteTimeoutMode, true, out var parsedDuo)
            && Enum.IsDefined(parsedDuo)
            ? parsedDuo
            : DuoVoteTimeoutMode.AutoReject;
        if (crew.DuoVoteTimeoutMode != duoMode)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.DuoVoteTimeoutMode,
                crew.DuoVoteTimeoutMode.ToString(),
                duoMode.ToString()));
        }

        if (crew.AutoResolveOverTime != request.AutoResolveOverTime)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.AutoResolveOverTime,
                crew.AutoResolveOverTime.ToString(),
                request.AutoResolveOverTime.ToString()));
        }

        if (crew.BaseAutoResolveHours != request.BaseAutoResolveHours)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.BaseAutoResolveHours,
                crew.BaseAutoResolveHours.ToString(),
                request.BaseAutoResolveHours.ToString()));
        }

        if (crew.ChangeAutoResolveTimerOnFirstReject != request.ChangeAutoResolveTimerOnFirstReject)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.ChangeAutoResolveTimerOnFirstReject,
                crew.ChangeAutoResolveTimerOnFirstReject.ToString(),
                request.ChangeAutoResolveTimerOnFirstReject.ToString()));
        }

        if (crew.AutoResolveHoursAfterFirstReject != request.AutoResolveHoursAfterFirstReject)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.AutoResolveHoursAfterFirstReject,
                crew.AutoResolveHoursAfterFirstReject.ToString(),
                request.AutoResolveHoursAfterFirstReject.ToString()));
        }

        if (crew.InNeedDefaultThreshold != request.InNeedDefaultThreshold)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.InNeedDefaultThreshold,
                crew.InNeedDefaultThreshold.ToString("0.##"),
                request.InNeedDefaultThreshold.ToString("0.##")));
        }

        if (crew.FinancialMembershipContributionFloor != request.FinancialMembershipContributionFloor)
        {
            changes.Add(new CrewSettingChangeItem(
                CrewSettingField.FinancialMembershipContributionFloor,
                crew.FinancialMembershipContributionFloor.ToString("0.##"),
                request.FinancialMembershipContributionFloor.ToString("0.##")));
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
    public const string DefaultTitle = "Change crew setting";

    public static string BuildTitle(CrewSettingChangeItem change) =>
        change.Field switch
        {
            CrewSettingField.Name => "Change crew name",
            CrewSettingField.MaxSize => "Change crew size limit",
            CrewSettingField.Privacy => "Change crew privacy",
            CrewSettingField.Scope => "Change crew location type",
            CrewSettingField.CountryCode => "Change crew country",
            CrewSettingField.AllowedZipCodes => "Change allowed postal codes",
            CrewSettingField.RadiusMiles => "Change distance setting (legacy)",
            CrewSettingField.AllowSurvivalThresholds => "Change survival thresholds setting",
            CrewSettingField.RequireApprovalForEdits => "Change edit-approval requirement",
            CrewSettingField.DuoVoteTimeoutMode => "Change tied-vote timeout",
            CrewSettingField.AutoResolveOverTime => "Change auto-resolve over time",
            CrewSettingField.BaseAutoResolveHours => "Change initial auto-resolve window",
            CrewSettingField.ChangeAutoResolveTimerOnFirstReject => "Change timer-on-first-reject setting",
            CrewSettingField.AutoResolveHoursAfterFirstReject => "Change post-reject auto-resolve window",
            CrewSettingField.InNeedDefaultThreshold => "Change in-need threshold",
            CrewSettingField.FinancialMembershipContributionFloor => "Change financial membership floor",
            CrewSettingField.LibraryOfThingsEnabled => "Change Library of Things setting",
            CrewSettingField.MemberCycleCapMode => "Change member cycle cap mode",
            CrewSettingField.MemberCycleCapFixedAmount => "Change member cycle cap amount",
            CrewSettingField.MemberCycleCapMultiplier => "Change member cycle cap multiplier",
            CrewSettingField.NonMemberCycleCapMode => "Change non-member cycle cap mode",
            CrewSettingField.NonMemberCycleCapFixedAmount => "Change non-member cycle cap amount",
            CrewSettingField.NonMemberCycleCapMultiplier => "Change non-member cycle cap multiplier",
            CrewSettingField.AllowCrewmateFileAttachments => "Change file-attachment permission",
            CrewSettingField.MinimumCrewmateTenureDaysForAttachments => "Change attachment tenure requirement",
            CrewSettingField.MinimumContributionForAttachments => "Change attachment contribution requirement",
            CrewSettingField.MinimumCrewmateTenureDaysForProposals => "Change proposal tenure requirement",
            CrewSettingField.MinimumContributionForProposals => "Change proposal contribution requirement",
            CrewSettingField.AllowCrossCrewGiving => "Change cross-crew giving setting",
            CrewSettingField.ImageResourceId => string.IsNullOrEmpty(change.NewValue)
                ? "Remove crew image"
                : "Change crew image",
            _ => DefaultTitle
        };

    public static string BuildDescription(CrewSettingChangeItem change) =>
        change.Field switch
        {
            CrewSettingField.Name =>
                $"Change the crew name from \"{change.OldValue}\" to \"{change.NewValue}\".",
            CrewSettingField.MaxSize =>
                $"Change the crew size limit from {change.OldValue} to {change.NewValue} members.",
            CrewSettingField.Privacy =>
                $"Change crew privacy from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.Scope =>
                $"Change crew location type from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.CountryCode =>
                $"Change country from \"{FormatCountry(change.OldValue)}\" to \"{FormatCountry(change.NewValue)}\".",
            CrewSettingField.AllowedZipCodes =>
                $"Change allowed postal codes from \"{ZipCodeList.FormatDisplay(ZipCodeList.Deserialize(change.OldValue))}\" to \"{ZipCodeList.FormatDisplay(ZipCodeList.Deserialize(change.NewValue))}\".",
            CrewSettingField.RadiusMiles =>
                $"Change distance from {change.OldValue} to {change.NewValue} miles (legacy; no longer applied).",
            CrewSettingField.AllowSurvivalThresholds =>
                $"Turn \"Allow survival thresholds\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            CrewSettingField.RequireApprovalForEdits =>
                $"Turn \"Require approval for crew edits\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            CrewSettingField.DuoVoteTimeoutMode =>
                $"Change tied-vote timeout (equal approve/reject when the timer expires) from \"{FormatDuoMode(change.OldValue)}\" to \"{FormatDuoMode(change.NewValue)}\".",
            CrewSettingField.AutoResolveOverTime =>
                $"Turn \"Auto-resolve over time\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}. " +
                "When enabled, the side with more votes wins at timer expiry; equal counts use tied-vote timeout.",
            CrewSettingField.BaseAutoResolveHours =>
                $"Change the initial auto-resolve window from {change.OldValue} to {change.NewValue} hours.",
            CrewSettingField.ChangeAutoResolveTimerOnFirstReject =>
                $"Turn \"Change auto-resolve timer on first reject\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            CrewSettingField.AutoResolveHoursAfterFirstReject =>
                $"Change the post-reject auto-resolve window from {change.OldValue} to {change.NewValue} hours.",
            CrewSettingField.InNeedDefaultThreshold =>
                $"Change the in-need threshold from ${change.OldValue} to ${change.NewValue}. " +
                "Crewmates whose 3-month average (excluding Library of Things) is at or below this cannot opt out of in-need.",
            CrewSettingField.FinancialMembershipContributionFloor =>
                $"Change the financial membership contribution floor from ${change.OldValue} to ${change.NewValue}. " +
                "Average includes Library of Things gifts.",
            CrewSettingField.LibraryOfThingsEnabled =>
                $"Turn \"Library of Things\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            CrewSettingField.MemberCycleCapMode =>
                $"Change member cycle cap mode from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.MemberCycleCapFixedAmount =>
                $"Change member fixed cycle cap from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.MemberCycleCapMultiplier =>
                $"Change member cycle cap multiplier from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.NonMemberCycleCapMode =>
                $"Change non-member cycle cap mode from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.NonMemberCycleCapFixedAmount =>
                $"Change non-member fixed cycle cap from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.NonMemberCycleCapMultiplier =>
                $"Change non-member cycle cap multiplier from {change.OldValue} to {change.NewValue}.",
            CrewSettingField.AllowCrewmateFileAttachments =>
                $"Turn \"Allow crewmate file attachments\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            CrewSettingField.MinimumCrewmateTenureDaysForAttachments =>
                $"Change minimum crewmate tenure for attachments from {change.OldValue} to {change.NewValue} days.",
            CrewSettingField.MinimumContributionForAttachments =>
                $"Change minimum contribution for attachments from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.MinimumCrewmateTenureDaysForProposals =>
                $"Change minimum crewmate tenure for proposals from {change.OldValue} to {change.NewValue} days.",
            CrewSettingField.MinimumContributionForProposals =>
                $"Change minimum contribution for proposals from ${change.OldValue} to ${change.NewValue}.",
            CrewSettingField.AllowCrossCrewGiving =>
                $"Turn \"Allow cross-crew giving\" from {FormatBool(change.OldValue)} to {FormatBool(change.NewValue)}.",
            CrewSettingField.ImageResourceId =>
                string.IsNullOrEmpty(change.NewValue)
                    ? "Remove the crew image."
                    : "Replace the crew image with a new one.",
            _ => "Change a crew setting."
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
