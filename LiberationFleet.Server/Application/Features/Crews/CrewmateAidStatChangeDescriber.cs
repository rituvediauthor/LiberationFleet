using System.Globalization;
using System.Text;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

/// <summary>
/// Human-readable titles/descriptions for crewmate aid-stat change proposals.
/// </summary>
public static class CrewmateAidStatChangeDescriber
{
    public const string DescriptionIntroPrefix = "Proposed mutual-aid changes for ";

    public sealed class CurrentAidSnapshot
    {
        public decimal EstimatedMonthlyContribution { get; init; }
        public decimal LifetimeContributions { get; init; }
        public decimal ReceptionThisYear { get; init; }
        public int PercentBoost { get; init; }
        public AidSeasonAccountingDto? SeasonAccounting { get; init; }
        public bool InNeedOfAid { get; init; } = true;
        public bool NeedsSurvivalAid { get; init; }
        public int EmergencyLevel { get; init; }
        public int DisabilityLevel { get; init; }
        public int PeopleRepresentedCount { get; init; } = 1;
        public string PaymentPlatformsJson { get; init; } = "[]";
    }

    public static string BuildTitle(string targetName, IReadOnlyList<CrewmateAidStatChangeItem> changes)
    {
        var hasSeason = changes.Any(c => c.Field == CrewmateAidStatField.SeasonAccounting);
        var preferenceFields = changes.Where(c => IsPreferenceField(c.Field)).ToList();
        var moneyScalars = changes
            .Where(c => c.Field != CrewmateAidStatField.SeasonAccounting && !IsPreferenceField(c.Field))
            .ToList();

        if (hasSeason && moneyScalars.Count == 0 && preferenceFields.Count == 0)
        {
            return $"Update season cycle & survival aid for {targetName}";
        }

        if (!hasSeason && moneyScalars.Count == 0 && preferenceFields.Count > 0)
        {
            if (preferenceFields.Count == 1)
            {
                return $"Change {FormatFieldLabel(preferenceFields[0].Field).ToLowerInvariant()} for {targetName}";
            }

            return $"Update mutual-aid preferences for {targetName}";
        }

        if (!hasSeason && moneyScalars.Count == 1 && preferenceFields.Count == 0)
        {
            return $"Change {FormatFieldLabel(moneyScalars[0].Field).ToLowerInvariant()} for {targetName}";
        }

        if (hasSeason || (moneyScalars.Count > 0 && preferenceFields.Count > 0))
        {
            return $"Update mutual-aid history for {targetName}";
        }

        return $"Update mutual-aid totals for {targetName}";
    }

    public static string BuildDescription(
        string targetName,
        IReadOnlyList<CrewmateAidStatChangeItem> changes,
        CurrentAidSnapshot current)
    {
        var sb = new StringBuilder();
        sb.AppendLine($"{DescriptionIntroPrefix}{targetName}:");
        sb.AppendLine();

        foreach (var item in changes.Where(c => c.Field != CrewmateAidStatField.SeasonAccounting))
        {
            AppendScalarChange(sb, item, current);
        }

        var seasonItem = changes.FirstOrDefault(c => c.Field == CrewmateAidStatField.SeasonAccounting);
        if (seasonItem is not null)
        {
            if (sb.Length > 0 && !sb.ToString().EndsWith(Environment.NewLine + Environment.NewLine, StringComparison.Ordinal))
            {
                sb.AppendLine();
            }

            AppendSeasonAccountingChange(sb, seasonItem.NewValue, current.SeasonAccounting);
        }

        return sb.ToString().TrimEnd();
    }

    public static string FormatFieldLabel(CrewmateAidStatField field) =>
        field switch
        {
            CrewmateAidStatField.EstimatedMonthlyContribution => "Estimated monthly contribution",
            CrewmateAidStatField.LifetimeContributions => "Lifetime contributions",
            CrewmateAidStatField.ReceptionThisYear => "Reception this year",
            CrewmateAidStatField.TotalReceptionAmount => "Total reception (season)",
            CrewmateAidStatField.SurvivalThresholdReceived => "Survival reception (season)",
            CrewmateAidStatField.CycleReceived => "Cycle received",
            CrewmateAidStatField.CycleCompleted => "Cycle completed",
            CrewmateAidStatField.PercentBoost => "Percent boost (this season)",
            CrewmateAidStatField.SeasonAccounting => "Season cycle & survival thresholds",
            CrewmateAidStatField.InNeedOfAid => "In need of aid",
            CrewmateAidStatField.NeedsSurvivalAid => "Needs survival aid",
            CrewmateAidStatField.EmergencyLevel => "Emergency level",
            CrewmateAidStatField.DisabilityLevel => "Level of disability",
            CrewmateAidStatField.PeopleRepresentedCount => "People represented",
            CrewmateAidStatField.PaymentPlatforms => "Payment platforms",
            _ => field.ToString()
        };

    public static bool IsPreferenceField(CrewmateAidStatField field) =>
        field is CrewmateAidStatField.InNeedOfAid
            or CrewmateAidStatField.NeedsSurvivalAid
            or CrewmateAidStatField.EmergencyLevel
            or CrewmateAidStatField.DisabilityLevel
            or CrewmateAidStatField.PeopleRepresentedCount
            or CrewmateAidStatField.PaymentPlatforms;

    private static void AppendScalarChange(
        StringBuilder sb,
        CrewmateAidStatChangeItem item,
        CurrentAidSnapshot current)
    {
        var label = FormatFieldLabel(item.Field);
        switch (item.Field)
        {
            case CrewmateAidStatField.EstimatedMonthlyContribution:
                sb.AppendLine($"• {label}: {Money(current.EstimatedMonthlyContribution)} → {Money(ParseDecimal(item.NewValue))}");
                break;
            case CrewmateAidStatField.LifetimeContributions:
                sb.AppendLine($"• {label}: {Money(current.LifetimeContributions)} → {Money(ParseDecimal(item.NewValue))}");
                break;
            case CrewmateAidStatField.ReceptionThisYear:
                sb.AppendLine($"• {label}: {Money(current.ReceptionThisYear)} → {Money(ParseDecimal(item.NewValue))}");
                break;
            case CrewmateAidStatField.PercentBoost:
                sb.AppendLine($"• {label}: +{current.PercentBoost}% → +{ParseInt(item.NewValue)}%");
                break;
            case CrewmateAidStatField.CycleReceived:
                sb.AppendLine($"• {label}: → {Money(ParseDecimal(item.NewValue))}");
                break;
            case CrewmateAidStatField.CycleCompleted:
                sb.AppendLine($"• {label}: → {(item.NewValue == "true" ? "Yes" : "No")}");
                break;
            case CrewmateAidStatField.InNeedOfAid:
                sb.AppendLine($"• {label}: {YesNo(current.InNeedOfAid)} → {YesNo(ParseBool(item.NewValue))}");
                break;
            case CrewmateAidStatField.NeedsSurvivalAid:
                sb.AppendLine($"• {label}: {YesNo(current.NeedsSurvivalAid)} → {YesNo(ParseBool(item.NewValue))}");
                break;
            case CrewmateAidStatField.EmergencyLevel:
                sb.AppendLine($"• {label}: {current.EmergencyLevel} → {ParseInt(item.NewValue)}");
                break;
            case CrewmateAidStatField.DisabilityLevel:
                sb.AppendLine($"• {label}: {current.DisabilityLevel} → {ParseInt(item.NewValue)}");
                break;
            case CrewmateAidStatField.PeopleRepresentedCount:
                sb.AppendLine($"• {label}: {current.PeopleRepresentedCount} → {ParseInt(item.NewValue)}");
                break;
            case CrewmateAidStatField.PaymentPlatforms:
                sb.AppendLine($"• {label}: {FormatPlatformsSummary(current.PaymentPlatformsJson)} → {FormatPlatformsSummary(item.NewValue)}");
                break;
            default:
                sb.AppendLine($"• {label}: → {item.NewValue}");
                break;
        }
    }

    public static string FormatPlatformsSummary(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return "(none)";
        }

        try
        {
            var rows = System.Text.Json.JsonSerializer.Deserialize<List<AidPaymentPlatformDraftDto>>(
                json,
                new System.Text.Json.JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                }) ?? [];
            if (rows.Count == 0)
            {
                return "(none)";
            }

            return string.Join(
                "; ",
                rows.Select(r =>
                {
                    var name = string.IsNullOrWhiteSpace(r.CustomPlatformName)
                        ? (string.IsNullOrWhiteSpace(r.PlatformName) ? $"#{r.PlatformId}" : r.PlatformName)
                        : r.CustomPlatformName.Trim();
                    var handle = string.IsNullOrWhiteSpace(r.Handle) ? "" : $" {r.Handle.Trim()}";
                    var preferred = r.IsPreferred ? " (preferred)" : "";
                    return $"{name}{handle}{preferred}";
                }));
        }
        catch (System.Text.Json.JsonException)
        {
            return "(invalid platforms)";
        }
    }

    private static bool ParseBool(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value == "1"
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    private static void AppendSeasonAccountingChange(
        StringBuilder sb,
        string newValueJson,
        AidSeasonAccountingDto? before)
    {
        var after = AidStatDraftSerializer.Deserialize(newValueJson);
        if (after is null)
        {
            sb.AppendLine("Season cycle & survival thresholds: (could not parse proposed values)");
            return;
        }

        before ??= new AidSeasonAccountingDto();

        sb.AppendLine("Season cycle:");
        AppendMoneyLine(sb, "Amount already received this cycle", before.CycleReceived, after.CycleReceived);

        var beforeCap = before.CycleCap;
        var afterCap = after.CycleCap;
        if (beforeCap != afterCap || afterCap.HasValue)
        {
            sb.AppendLine(
                $"• Cycle target (total cap): {MoneyOrDash(beforeCap)} → {MoneyOrDash(afterCap)}");
        }

        var beforeRemaining = Math.Max(0m, (beforeCap ?? before.CycleReceived) - before.CycleReceived);
        var afterRemaining = Math.Max(0m, (afterCap ?? after.CycleReceived) - after.CycleReceived);
        if (beforeCap.HasValue || afterCap.HasValue)
        {
            AppendMoneyLine(sb, "Still owed this cycle", beforeRemaining, afterRemaining);
        }

        AppendBoolLine(sb, "Cycle is active (receiving now)", before.HasActiveCycle, after.HasActiveCycle);
        AppendOrderLine(sb, "Reception order (1 = front of queue)", before.ReceptionOrder, after.ReceptionOrder);
        AppendBoolLine(sb, "Auto-join when the next season starts", before.AutoJoinSeasonOnStart, after.AutoJoinSeasonOnStart);

        sb.AppendLine();
        sb.AppendLine("Survival thresholds:");

        var beforeById = before.SurvivalThresholds
            .Where(t => t.Id.HasValue)
            .ToDictionary(t => t.Id!.Value);
        var afterIds = after.SurvivalThresholds.Where(t => t.Id.HasValue).Select(t => t.Id!.Value).ToHashSet();
        var removedIds = after.RemovedThresholdIds.Distinct().ToList();
        if (removedIds.Count == 0)
        {
            removedIds = beforeById.Keys.Where(id => !afterIds.Contains(id)).ToList();
        }

        var anyThresholdLine = false;
        foreach (var id in removedIds)
        {
            if (!beforeById.TryGetValue(id, out var removed))
            {
                sb.AppendLine($"• Remove survival threshold #{id}");
                anyThresholdLine = true;
                continue;
            }

            sb.AppendLine(
                $"• Remove survival threshold (was order #{removed.Order}: need {Money(removed.ThresholdAmount)}, " +
                $"still owed {Money(removed.AmountRemaining)})");
            anyThresholdLine = true;
        }

        foreach (var row in after.SurvivalThresholds.OrderBy(t => t.Order <= 0 ? int.MaxValue : t.Order))
        {
            var received = Math.Max(0m, row.ThresholdAmount - row.AmountRemaining);
            if (row.Id is int id && beforeById.TryGetValue(id, out var prior))
            {
                var priorReceived = Math.Max(0m, prior.ThresholdAmount - prior.AmountRemaining);
                var changed =
                    prior.ThresholdAmount != row.ThresholdAmount
                    || prior.AmountRemaining != row.AmountRemaining
                    || prior.Order != row.Order
                    || prior.Year != row.Year
                    || prior.Month != row.Month;
                if (!changed)
                {
                    continue;
                }

                sb.AppendLine(
                    $"• Adjust survival threshold #{id}: " +
                    $"month {FormatThresholdMonth(prior)} → {FormatThresholdMonth(row)}; " +
                    $"order #{prior.Order} → #{row.Order}; " +
                    $"need {Money(prior.ThresholdAmount)} → {Money(row.ThresholdAmount)}; " +
                    $"received {Money(priorReceived)} → {Money(received)}; " +
                    $"still owed {Money(prior.AmountRemaining)} → {Money(row.AmountRemaining)}");
                anyThresholdLine = true;
            }
            else
            {
                sb.AppendLine(
                    $"• Add survival threshold ({FormatThresholdMonth(row)}): order #{row.Order}, need {Money(row.ThresholdAmount)}, " +
                    $"received {Money(received)}, still owed {Money(row.AmountRemaining)}");
                anyThresholdLine = true;
            }
        }

        if (!anyThresholdLine)
        {
            if (after.SurvivalThresholds.Count == 0 && removedIds.Count == 0)
            {
                sb.AppendLine("• (no survival thresholds)");
            }
            else
            {
                sb.AppendLine("• (no survival-threshold amount/order changes)");
            }
        }
    }

    private static void AppendMoneyLine(StringBuilder sb, string label, decimal before, decimal after)
    {
        if (before == after)
        {
            sb.AppendLine($"• {label}: {Money(after)} (unchanged)");
            return;
        }

        sb.AppendLine($"• {label}: {Money(before)} → {Money(after)}");
    }

    private static void AppendBoolLine(StringBuilder sb, string label, bool before, bool after)
    {
        if (before == after)
        {
            sb.AppendLine($"• {label}: {YesNo(after)} (unchanged)");
            return;
        }

        sb.AppendLine($"• {label}: {YesNo(before)} → {YesNo(after)}");
    }

    private static void AppendOrderLine(StringBuilder sb, string label, int? before, int? after)
    {
        if (before == after)
        {
            sb.AppendLine($"• {label}: {OrderText(after)} (unchanged)");
            return;
        }

        sb.AppendLine($"• {label}: {OrderText(before)} → {OrderText(after)}");
    }

    private static string Money(decimal value) =>
        "$" + value.ToString("0.00", CultureInfo.InvariantCulture);

    private static string MoneyOrDash(decimal? value) =>
        value.HasValue ? Money(value.Value) : "(none)";

    private static string YesNo(bool value) => value ? "Yes" : "No";

    private static string OrderText(int? order) =>
        order is int o && o > 0 ? $"#{o}" : "(none)";

    private static string FormatThresholdMonth(AidSurvivalThresholdDraftDto row) =>
        row.Year is int y && row.Month is int m && m is >= 1 and <= 12
            ? $"{m:D2}/{y}"
            : "(month unset)";

    private static decimal ParseDecimal(string value) =>
        decimal.Parse(value, CultureInfo.InvariantCulture);

    private static int ParseInt(string value) =>
        int.Parse(value, CultureInfo.InvariantCulture);
}
