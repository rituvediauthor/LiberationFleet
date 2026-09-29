using System.Globalization;
using System.Text;
using System.Text.Json;
using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Crews;

public sealed class CrewmateAidStatChangeItem
{
    public CrewmateAidStatField Field { get; init; }
    public string NewValue { get; init; } = string.Empty;
}

public sealed class CrewmateAidStatProposalResult
{
    public bool Success { get; init; }
    public string Message { get; init; } = string.Empty;
    public int ProposalId { get; init; }

    public static CrewmateAidStatProposalResult Succeeded(int proposalId, string message) =>
        new() { Success = true, ProposalId = proposalId, Message = message };

    public static CrewmateAidStatProposalResult Failed(string message, int proposalId = 0) =>
        new() { Success = false, Message = message, ProposalId = proposalId };
}

public class CrewmateAidStatProposalService(
    IProposalRepository proposalRepository,
    IFleetRepository fleetRepository,
    ICrewRepository crewRepository,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase
    };

    public async Task<CrewmateAidStatProposalResult> CreateAsync(
        int crewId,
        int authorUserId,
        int targetUserId,
        IReadOnlyList<CrewmateAidStatChangeItem> changes,
        CancellationToken cancellationToken)
    {
        if (changes.Count == 0)
        {
            return CrewmateAidStatProposalResult.Failed("At least one aid statistic change is required.");
        }

        var authorMembership = await membershipRepository.GetMembershipAsync(authorUserId, crewId, cancellationToken);
        if (authorMembership is null || authorMembership.IsBanned
            || !CrewRoleAuthorizationService.CanProposeCrewmateAidStatEdits(authorMembership))
        {
            return CrewmateAidStatProposalResult.Failed(
                "Only organizers and accountants can propose aid statistic edits.");
        }

        var crewForAuth = await crewRepository.GetByIdAsync(crewId, cancellationToken);
        if (crewForAuth is null)
        {
            return CrewmateAidStatProposalResult.Failed("Crew not found.");
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(targetUserId, cancellationToken);
        if (targetUser is null)
        {
            return CrewmateAidStatProposalResult.Failed("Crewmate not found.");
        }

        var targetMembership = await membershipRepository.GetMembershipAsync(targetUserId, crewId, cancellationToken);
        if (targetMembership is null || targetMembership.IsBanned)
        {
            return CrewmateAidStatProposalResult.Failed("Crewmate not found.");
        }

        var normalized = new List<CrewmateAidStatChangeItem>();
        foreach (var change in changes)
        {
            if (change.Field == CrewmateAidStatField.CycleCompleted)
            {
                return CrewmateAidStatProposalResult.Failed(
                    "Cycle completion is derived from cycle reception versus the cycle cap and cannot be set manually.");
            }

            if (change.Field is CrewmateAidStatField.TotalReceptionAmount
                or CrewmateAidStatField.SurvivalThresholdReceived)
            {
                return CrewmateAidStatProposalResult.Failed(
                    "Total and survival reception are derived from cycle reception and survival thresholds. Use SeasonAccounting instead.");
            }

            if (!TryNormalizeValue(change.Field, change.NewValue, out var normalizedValue, out var error))
            {
                return CrewmateAidStatProposalResult.Failed(error);
            }

            normalized.Add(new CrewmateAidStatChangeItem
            {
                Field = change.Field,
                NewValue = normalizedValue
            });
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        var hasSeasonAccounting = normalized.Any(i => i.Field == CrewmateAidStatField.SeasonAccounting);
        var hasLegacyCycleFields = normalized.Any(i => IsLegacyCycleField(i.Field));

        if (hasLegacyCycleFields && !hasSeasonAccounting && crew?.CurrentSeasonStartDate is null)
        {
            return CrewmateAidStatProposalResult.Failed(
                "Season cycle fields can only be edited after the crew has started a season, or via SeasonAccounting (pre-season draft).");
        }

        if (crew?.CurrentSeasonStartDate is DateTime seasonStart
            && (hasSeasonAccounting || hasLegacyCycleFields))
        {
            var cycleReceived = ResolveProposedCycleReceived(normalized);
            if (cycleReceived is decimal received)
            {
                var cycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                    crewId,
                    targetUserId,
                    seasonStart,
                    cancellationToken);
                if (cycle is not null)
                {
                    var isMember = await mutualAidService.IsFinancialMemberAsync(
                        targetUserId,
                        crewId,
                        targetMembership,
                        cancellationToken);
                    var effectiveCap = EmergencySplitService.ResolveSegmentCap(
                        cycle,
                        isMember,
                        crew.SeasonMemberCycleCap,
                        crew.SeasonNonMemberCycleCap);
                    if (received > effectiveCap)
                    {
                        return CrewmateAidStatProposalResult.Failed(
                            $"Cycle reception cannot exceed the effective cycle cap (${effectiveCap.ToString(CultureInfo.InvariantCulture)}).");
                    }
                }
            }
        }

        var pending = await proposalRepository.GetPendingCrewmateAidStatChangeForTargetAsync(
            crewId,
            targetUserId,
            cancellationToken);
        if (pending is not null)
        {
            return CrewmateAidStatProposalResult.Failed(
                "An aid statistic change proposal for this crewmate is already pending.",
                pending.ProposalId);
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = crewId,
            AuthorUserId = authorUserId,
            Kind = ProposalKind.CrewmateAidStatChange,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };

        await ProposalVotingService.ApplyTimerRulesOnCreateAsync(
            proposal, utcNow, crewRepository, fleetRepository, cancellationToken);
        await proposalRepository.AddProposalAsync(proposal, cancellationToken);

        var descriptionBuilder = new StringBuilder();
        descriptionBuilder.AppendLine($"Adjust mutual-aid accounting for {targetUser.Username}:");
        foreach (var item in normalized)
        {
            descriptionBuilder.AppendLine($"• {FormatFieldLabel(item.Field)} → {FormatDisplayValue(item.Field, item.NewValue)}");
        }

        await proposalRepository.AddCrewmateAidStatChangeAsync(new ProposalCrewmateAidStatChange
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            ChangesJson = JsonSerializer.Serialize(normalized, JsonOptions),
            Title = $"Update aid stats for {targetUser.Username}",
            Description = descriptionBuilder.ToString().Trim()
        }, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await ProposalVotingService.EnsureAuthorApproveVoteAsync(
            proposalRepository,
            proposal,
            utcNow,
            cancellationToken);
        var statusBefore = proposal.Status;
        await ProposalVotingService.RecalculateAfterAuthorVoteAsync(
            proposal,
            proposalRepository,
            fleetRepository,
            crewRepository,
            utcNow,
            cancellationToken);
        if (statusBefore != ProposalStatus.Approved && proposal.Status == ProposalStatus.Approved)
        {
            await TryApplyApprovedProposalAsync(proposal, cancellationToken);
        }
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            $"A proposal was submitted to update aid statistics for {targetUser.Username}.",
            ProposalRouting.StatusListUrl(proposal),
            relatedEntityId: proposal.Id,
            excludeUserId: authorUserId,
            cancellationToken: cancellationToken);

        return CrewmateAidStatProposalResult.Succeeded(proposal.Id, "Aid statistic change proposal submitted.");
    }

    public async Task TryApplyApprovedProposalAsync(Proposal proposal, CancellationToken cancellationToken)
    {
        if (proposal.Kind != ProposalKind.CrewmateAidStatChange || proposal.Status != ProposalStatus.Approved)
        {
            return;
        }

        var change = await proposalRepository.GetCrewmateAidStatChangeByProposalIdAsync(proposal.Id, cancellationToken);
        if (change is null || change.IsApplied || !proposal.CrewId.HasValue)
        {
            return;
        }

        var membership = await membershipRepository.GetMembershipAsync(
            change.TargetUserId,
            proposal.CrewId.Value,
            cancellationToken);
        if (membership is null || membership.IsBanned)
        {
            change.IsApplied = true;
            change.Description = $"{change.Description}\n(Could not apply: crewmate no longer in the crew.)";
            return;
        }

        List<CrewmateAidStatChangeItem> items;
        try
        {
            items = JsonSerializer.Deserialize<List<CrewmateAidStatChangeItem>>(change.ChangesJson, JsonOptions) ?? [];
        }
        catch (JsonException)
        {
            change.IsApplied = true;
            change.Description = $"{change.Description}\n(Could not apply: invalid change payload.)";
            return;
        }

        var seasonAccountingItem = items.FirstOrDefault(i => i.Field == CrewmateAidStatField.SeasonAccounting);
        if (seasonAccountingItem is not null)
        {
            var accounting = AidStatDraftSerializer.Deserialize(seasonAccountingItem.NewValue);
            if (accounting is null)
            {
                change.IsApplied = true;
                change.Description = $"{change.Description}\n(Could not apply: invalid season accounting payload.)";
                return;
            }

            await mutualAidService.ApplyAidSeasonAccountingAsync(
                proposal.CrewId.Value,
                membership,
                accounting,
                persistAsDraftWhenNoSeason: true,
                cancellationToken);
        }

        var crew = await mutualAidRepository.GetCrewAsync(proposal.CrewId.Value, cancellationToken);
        SeasonCycle? cycle = null;
        var isFinancialMember = false;
        decimal effectiveCap = 0m;
        var legacyCycleItems = items.Where(i => IsLegacyCycleField(i.Field)).ToList();
        if (crew?.CurrentSeasonStartDate is DateTime seasonStart
            && legacyCycleItems.Count > 0
            && seasonAccountingItem is null)
        {
            isFinancialMember = await mutualAidService.IsFinancialMemberAsync(
                change.TargetUserId,
                proposal.CrewId.Value,
                membership,
                cancellationToken);

            cycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                proposal.CrewId.Value,
                change.TargetUserId,
                seasonStart,
                cancellationToken);
            if (cycle is null)
            {
                await mutualAidService.EnsurePrimarySeasonCycleExistsAsync(
                    proposal.CrewId.Value,
                    membership,
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                cycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                    proposal.CrewId.Value,
                    change.TargetUserId,
                    seasonStart,
                    cancellationToken);
            }

            if (cycle is null)
            {
                change.IsApplied = true;
                change.Description = $"{change.Description}\n(Could not apply: no primary season cycle available.)";
                return;
            }

            effectiveCap = EmergencySplitService.ResolveSegmentCap(
                cycle,
                isFinancialMember,
                crew.SeasonMemberCycleCap,
                crew.SeasonNonMemberCycleCap);

            var cycleReceivedItem = items.FirstOrDefault(i => i.Field == CrewmateAidStatField.CycleReceived);
            if (cycleReceivedItem is not null)
            {
                var received = decimal.Parse(cycleReceivedItem.NewValue, CultureInfo.InvariantCulture);
                if (received > effectiveCap)
                {
                    change.IsApplied = true;
                    change.Description =
                        $"{change.Description}\n(Could not apply: cycle reception ${received.ToString(CultureInfo.InvariantCulture)} exceeds effective cap ${effectiveCap.ToString(CultureInfo.InvariantCulture)}.)";
                    return;
                }
            }
        }

        var utcNow = DateTime.UtcNow;
        foreach (var item in items)
        {
            if (item.Field is CrewmateAidStatField.CycleCompleted
                or CrewmateAidStatField.SeasonAccounting
                or CrewmateAidStatField.TotalReceptionAmount
                or CrewmateAidStatField.SurvivalThresholdReceived)
            {
                continue;
            }

            ApplyMembershipOrLegacyCycleChange(membership, cycle, item);
        }

        if (cycle is not null && legacyCycleItems.Count > 0 && seasonAccountingItem is null)
        {
            SyncCycleCompletedFromReception(cycle, effectiveCap, utcNow);
        }

        change.IsApplied = true;

        await mutualAidService.OnCrewContributionsChangedAsync(proposal.CrewId.Value, cancellationToken);
        await mutualAidService.TryEndSeasonIfCompleteAsync(proposal.CrewId.Value, cancellationToken);
    }

    private static decimal? ResolveProposedCycleReceived(IReadOnlyList<CrewmateAidStatChangeItem> items)
    {
        var accountingItem = items.FirstOrDefault(i => i.Field == CrewmateAidStatField.SeasonAccounting);
        if (accountingItem is not null)
        {
            var accounting = AidStatDraftSerializer.Deserialize(accountingItem.NewValue);
            return accounting?.CycleReceived;
        }

        var cycleReceivedItem = items.FirstOrDefault(i => i.Field == CrewmateAidStatField.CycleReceived);
        return cycleReceivedItem is null
            ? null
            : decimal.Parse(cycleReceivedItem.NewValue, CultureInfo.InvariantCulture);
    }

    private static bool IsLegacyCycleField(CrewmateAidStatField field) =>
        field is CrewmateAidStatField.TotalReceptionAmount
            or CrewmateAidStatField.SurvivalThresholdReceived
            or CrewmateAidStatField.CycleReceived
            or CrewmateAidStatField.CycleCompleted;

    private static void ApplyMembershipOrLegacyCycleChange(
        CrewMembership membership,
        SeasonCycle? cycle,
        CrewmateAidStatChangeItem item)
    {
        switch (item.Field)
        {
            case CrewmateAidStatField.EstimatedMonthlyContribution:
                membership.EstimatedMonthlyContribution = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.LifetimeContributions:
                membership.LifetimeContributionOverride = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.ReceptionThisYear:
                membership.ReceptionThisYearOverride = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.CycleReceived when cycle is not null:
                cycle.CycleReceived = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.PercentBoost:
                membership.PercentBonus = int.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
        }
    }

    private static void SyncCycleCompletedFromReception(SeasonCycle cycle, decimal effectiveCap, DateTime utcNow)
    {
        if (MutualAidCalculationService.IsCycleSatisfied(cycle, effectiveCap))
        {
            cycle.CycleCompleted = true;
            cycle.CycleCompletedAt ??= utcNow;
            cycle.CycleCapAtCompletion = effectiveCap > 0m ? effectiveCap : cycle.CycleCapAtStart;
            return;
        }

        cycle.CycleCompleted = false;
        cycle.CycleCompletedAt = null;
    }

    private static bool TryNormalizeValue(
        CrewmateAidStatField field,
        string raw,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;
        var trimmed = (raw ?? string.Empty).Trim();

        if (field == CrewmateAidStatField.CycleCompleted)
        {
            error = "Cycle completion is derived from cycle reception versus the cycle cap and cannot be set manually.";
            return false;
        }

        if (field == CrewmateAidStatField.SeasonAccounting)
        {
            var accounting = AidStatDraftSerializer.Deserialize(trimmed);
            if (accounting is null)
            {
                error = "Season accounting payload is invalid.";
                return false;
            }

            if (accounting.CycleReceived < 0)
            {
                error = "Cycle reception cannot be negative.";
                return false;
            }

            if (accounting.ReceptionOrder is < 1)
            {
                error = "Reception order must be 1 or greater.";
                return false;
            }

            foreach (var threshold in accounting.SurvivalThresholds)
            {
                if (threshold.AmountRemaining < 0 || threshold.ThresholdAmount < 0)
                {
                    error = "Survival threshold amounts cannot be negative.";
                    return false;
                }
            }

            normalized = AidStatDraftSerializer.Serialize(accounting);
            return true;
        }

        if (field == CrewmateAidStatField.PercentBoost)
        {
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var percent)
                && !int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out percent))
            {
                error = "Percent boost must be a whole number.";
                return false;
            }

            if (percent < 0 || percent > 1000)
            {
                error = "Percent boost must be between 0 and 1000.";
                return false;
            }

            normalized = percent.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (!decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount)
            && !decimal.TryParse(trimmed, NumberStyles.Number, CultureInfo.CurrentCulture, out amount))
        {
            error = $"{FormatFieldLabel(field)} must be a number.";
            return false;
        }

        if (amount < 0)
        {
            error = $"{FormatFieldLabel(field)} cannot be negative.";
            return false;
        }

        normalized = amount.ToString(CultureInfo.InvariantCulture);
        return true;
    }

    private static string FormatFieldLabel(CrewmateAidStatField field) =>
        field switch
        {
            CrewmateAidStatField.EstimatedMonthlyContribution => "Estimated monthly contribution",
            CrewmateAidStatField.LifetimeContributions => "Lifetime contributions",
            CrewmateAidStatField.ReceptionThisYear => "Reception this year",
            CrewmateAidStatField.TotalReceptionAmount => "Total reception (season)",
            CrewmateAidStatField.SurvivalThresholdReceived => "Survival reception (season)",
            CrewmateAidStatField.CycleReceived => "Cycle reception (season)",
            CrewmateAidStatField.CycleCompleted => "Cycle completed",
            CrewmateAidStatField.PercentBoost => "Percent boost (this season)",
            CrewmateAidStatField.SeasonAccounting => "Season accounting",
            _ => field.ToString()
        };

    private static string FormatDisplayValue(CrewmateAidStatField field, string value)
    {
        if (field == CrewmateAidStatField.SeasonAccounting)
        {
            var accounting = AidStatDraftSerializer.Deserialize(value);
            if (accounting is null)
            {
                return "(season accounting)";
            }

            return
                $"cycle ${accounting.CycleReceived:0.##}, active={(accounting.HasActiveCycle ? "yes" : "no")}, " +
                $"order={(accounting.ReceptionOrder?.ToString(CultureInfo.InvariantCulture) ?? "—")}, " +
                $"auto-join={(accounting.AutoJoinSeasonOnStart ? "yes" : "no")}, " +
                $"thresholds={accounting.SurvivalThresholds.Count}";
        }

        return field switch
        {
            CrewmateAidStatField.CycleCompleted => value == "true" ? "Yes" : "No",
            CrewmateAidStatField.PercentBoost => $"+{value}%",
            _ => $"${value}"
        };
    }
}
