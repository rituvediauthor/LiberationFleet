using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
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
    IGiftRepository giftRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork)
{
    private static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        // Accept both numeric and string enum values in ChangesJson (API uses strings).
        Converters = { new JsonStringEnumConverter() }
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

        // Placeholders: organizers/accountants apply mutual-aid edits immediately (no crew vote),
        // matching honorary-membership and merge-with behavior.
        if (targetMembership.IsPlaceholderMember
            && CrewRoleAuthorizationService.CanManagePlaceholders(authorMembership))
        {
            var applyNote = await ApplyNormalizedChangesAsync(
                crewId,
                targetUserId,
                targetMembership,
                normalized,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            var message = applyNote is null
                ? "Placeholder mutual-aid updates applied."
                : $"Placeholder mutual-aid updates applied. ({applyNote})";
            return CrewmateAidStatProposalResult.Succeeded(0, message);
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

        var targetName = UserDisplay.Name(targetUser);
        var currentSnapshot = await BuildCurrentAidSnapshotAsync(
            crewId,
            targetUserId,
            targetMembership,
            cancellationToken);
        var title = CrewmateAidStatChangeDescriber.BuildTitle(targetName, normalized);
        var description = CrewmateAidStatChangeDescriber.BuildDescription(
            targetName,
            normalized,
            currentSnapshot);

        await proposalRepository.AddCrewmateAidStatChangeAsync(new ProposalCrewmateAidStatChange
        {
            Proposal = proposal,
            TargetUserId = targetUserId,
            ChangesJson = JsonSerializer.Serialize(normalized, JsonOptions),
            Title = title,
            Description = description
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
            try
            {
                await TryApplyApprovedProposalAsync(proposal, cancellationToken);
            }
            finally
            {
                // Persist any membership overrides even if a later refresh step throws;
                // ProposalApprovalCoordinator will retry unfinished applies on next read.
                await unitOfWork.SaveChangesAsync(cancellationToken);
            }
        }
        else
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        await notificationService.NotifyCrewAsync(
            crewId,
            NotificationKind.NewProposal,
            "New proposal",
            NotificationPreview.BodyOrFallback(description, $"Mutual-aid changes proposed for {targetName}."),
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
            change.Description = AppendApplyNote(change.Description, "Could not apply: crewmate no longer in the crew.");
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
            change.Description = AppendApplyNote(change.Description, "Could not apply: invalid change payload.");
            return;
        }

        var applyNote = await ApplyNormalizedChangesAsync(
            proposal.CrewId.Value,
            change.TargetUserId,
            membership,
            items,
            cancellationToken);
        change.IsApplied = true;
        if (applyNote is not null)
        {
            change.Description = AppendApplyNote(change.Description, applyNote);
        }
    }

    /// <summary>
    /// Applies normalized aid-stat / preference items to the target membership and user.
    /// Returns a non-null note when apply partially failed (caller may surface it).
    /// </summary>
    private async Task<string?> ApplyNormalizedChangesAsync(
        int crewId,
        int targetUserId,
        CrewMembership membership,
        IReadOnlyList<CrewmateAidStatChangeItem> items,
        CancellationToken cancellationToken)
    {
        // Membership scalar overrides (lifetime, reception this year, estimate, percent boost)
        // must apply even when season-accounting / legacy cycle setup fails or early-returns.
        foreach (var item in items)
        {
            ApplyMembershipScalarChange(membership, item);
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(targetUserId, cancellationToken);
        var preferenceItems = items.Where(i => CrewmateAidStatChangeDescriber.IsPreferenceField(i.Field)).ToList();
        var previousInNeed = targetUser?.InNeedOfAid;
        var previousNeedsSurvival = targetUser?.NeedsSurvivalAid;
        var previousEmergency = targetUser?.EmergencyLevel;
        var previousDisability = targetUser?.DisabilityLevel;
        var previousPeople = targetUser?.PeopleRepresentedCount;
        string? applyNote = null;

        if (targetUser is not null && preferenceItems.Count > 0)
        {
            foreach (var item in preferenceItems)
            {
                if (item.Field == CrewmateAidStatField.PaymentPlatforms)
                {
                    var platformsApplied = await TryApplyPaymentPlatformsAsync(
                        crewId,
                        targetUser,
                        item.NewValue,
                        cancellationToken);
                    if (!platformsApplied)
                    {
                        applyNote =
                            "Could not apply payment platforms: invalid payload. Other overrides were applied.";
                        await mutualAidService.OnCrewContributionsChangedAsync(crewId, cancellationToken);
                        return applyNote;
                    }

                    continue;
                }

                ApplyUserPreferenceChange(targetUser, item);
            }
        }

        var seasonAccountingItem = items.FirstOrDefault(i => i.Field == CrewmateAidStatField.SeasonAccounting);
        if (seasonAccountingItem is not null)
        {
            var accounting = AidStatDraftSerializer.Deserialize(seasonAccountingItem.NewValue);
            if (accounting is null)
            {
                applyNote =
                    "Could not apply season accounting: invalid payload. Membership overrides were applied.";
                await mutualAidService.OnCrewContributionsChangedAsync(crewId, cancellationToken);
                return applyNote;
            }

            await mutualAidService.ApplyAidSeasonAccountingAsync(
                crewId,
                membership,
                accounting,
                persistAsDraftWhenNoSeason: true,
                cancellationToken);
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        var legacyCycleItems = items.Where(i => IsLegacyCycleField(i.Field)).ToList();
        if (crew?.CurrentSeasonStartDate is DateTime seasonStart
            && legacyCycleItems.Count > 0
            && seasonAccountingItem is null)
        {
            var isFinancialMember = await mutualAidService.IsFinancialMemberAsync(
                targetUserId,
                crewId,
                membership,
                cancellationToken);

            var cycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                crewId,
                targetUserId,
                seasonStart,
                cancellationToken);
            if (cycle is null)
            {
                await mutualAidService.EnsurePrimarySeasonCycleExistsAsync(
                    crewId,
                    membership,
                    cancellationToken);
                await unitOfWork.SaveChangesAsync(cancellationToken);
                cycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                    crewId,
                    targetUserId,
                    seasonStart,
                    cancellationToken);
            }

            if (cycle is null)
            {
                applyNote =
                    "Could not apply legacy cycle fields: no primary season cycle available. Membership overrides were applied.";
                await mutualAidService.OnCrewContributionsChangedAsync(crewId, cancellationToken);
                return applyNote;
            }

            var effectiveCap = EmergencySplitService.ResolveSegmentCap(
                cycle,
                isFinancialMember,
                crew.SeasonMemberCycleCap,
                crew.SeasonNonMemberCycleCap);

            foreach (var item in legacyCycleItems)
            {
                ApplyLegacyCycleChange(cycle, item);
            }

            SyncCycleCompletedFromReception(cycle, effectiveCap, DateTime.UtcNow);
        }

        if (targetUser is not null && preferenceItems.Count > 0)
        {
            if (previousInNeed != targetUser.InNeedOfAid)
            {
                await mutualAidService.OnInNeedOfAidChangedAsync(
                    targetUserId,
                    targetUser.InNeedOfAid,
                    cancellationToken);
            }

            if (previousNeedsSurvival != targetUser.NeedsSurvivalAid && targetUser.NeedsSurvivalAid)
            {
                await mutualAidService.EnsureCurrentMonthSurvivalThresholdsAsync(
                    targetUserId,
                    cancellationToken);
            }

            var priorityRelevant =
                previousEmergency != targetUser.EmergencyLevel
                || previousDisability != targetUser.DisabilityLevel
                || previousPeople != targetUser.PeopleRepresentedCount
                || previousInNeed != targetUser.InNeedOfAid;
            if (priorityRelevant)
            {
                await mutualAidService.OnCrewmatePriorityChangedAsync(targetUserId, cancellationToken);
            }
        }

        await mutualAidService.OnCrewContributionsChangedAsync(crewId, cancellationToken);
        await mutualAidService.TryEndSeasonIfCompleteAsync(crewId, cancellationToken);
        return applyNote;
    }

    private static bool IsLegacyCycleField(CrewmateAidStatField field) =>
        field is CrewmateAidStatField.TotalReceptionAmount
            or CrewmateAidStatField.SurvivalThresholdReceived
            or CrewmateAidStatField.CycleReceived
            or CrewmateAidStatField.CycleCompleted;

    private static void ApplyMembershipScalarChange(
        CrewMembership membership,
        CrewmateAidStatChangeItem item)
    {
        switch (item.Field)
        {
            case CrewmateAidStatField.EstimatedMonthlyContribution:
                membership.EstimatedMonthlyContribution = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.LifetimeContributions:
                // Explicit override (including 0) so gift-sum totals no longer win on display.
                membership.LifetimeContributionOverride = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.ReceptionThisYear:
                membership.ReceptionThisYearOverride = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.PercentBoost:
                membership.PercentBonus = int.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
        }
    }

    private static void ApplyUserPreferenceChange(User user, CrewmateAidStatChangeItem item)
    {
        switch (item.Field)
        {
            case CrewmateAidStatField.InNeedOfAid:
                user.InNeedOfAid = ParseBool(item.NewValue);
                break;
            case CrewmateAidStatField.NeedsSurvivalAid:
                user.NeedsSurvivalAid = ParseBool(item.NewValue);
                break;
            case CrewmateAidStatField.EmergencyLevel:
                user.EmergencyLevel = int.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.DisabilityLevel:
                user.DisabilityLevel = int.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
            case CrewmateAidStatField.PeopleRepresentedCount:
                user.PeopleRepresentedCount = int.Parse(item.NewValue, CultureInfo.InvariantCulture);
                break;
        }
    }

    private async Task<bool> TryApplyPaymentPlatformsAsync(
        int crewId,
        User user,
        string platformsJson,
        CancellationToken cancellationToken)
    {
        List<AidPaymentPlatformDraftDto>? drafts;
        try
        {
            drafts = JsonSerializer.Deserialize<List<AidPaymentPlatformDraftDto>>(platformsJson, JsonOptions);
        }
        catch (JsonException)
        {
            return false;
        }

        if (drafts is null)
        {
            return false;
        }

        var paymentPlatforms = drafts
            .Where(p => !string.IsNullOrWhiteSpace(p.Handle)
                && (p.PlatformId > 0 || !string.IsNullOrWhiteSpace(p.CustomPlatformName)))
            .ToList();

        user.PaymentPlatforms.Clear();
        var preferredAssigned = false;

        foreach (var platform in paymentPlatforms)
        {
            CrewPaymentPlatform crewPlatform;
            if (!string.IsNullOrWhiteSpace(platform.CustomPlatformName))
            {
                crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                    crewPaymentPlatformRepository,
                    unitOfWork,
                    crewId,
                    platform.CustomPlatformName,
                    cancellationToken);
            }
            else
            {
                var existing = await crewPaymentPlatformRepository.GetByIdAsync(platform.PlatformId, cancellationToken);
                if (existing is not null
                    && existing.CrewId == crewId
                    && !existing.IsLibraryOfThings)
                {
                    crewPlatform = existing;
                }
                else if (!string.IsNullOrWhiteSpace(platform.PlatformName))
                {
                    crewPlatform = await CrewPaymentPlatformService.EnsurePlatformAsync(
                        crewPaymentPlatformRepository,
                        unitOfWork,
                        crewId,
                        platform.PlatformName,
                        cancellationToken);
                }
                else
                {
                    return false;
                }
            }

            var isPreferred = platform.IsPreferred && !preferredAssigned;
            if (isPreferred)
            {
                preferredAssigned = true;
            }

            user.PaymentPlatforms.Add(new UserPaymentPlatform
            {
                CrewPaymentPlatformId = crewPlatform.Id,
                PlatformName = crewPlatform.Name,
                Handle = platform.Handle.Trim(),
                IsPreferred = isPreferred
            });
        }

        if (!preferredAssigned && user.PaymentPlatforms.Count > 0)
        {
            user.PaymentPlatforms.First().IsPreferred = true;
        }

        return true;
    }

    private static bool ParseBool(string value) =>
        value.Equals("true", StringComparison.OrdinalIgnoreCase)
        || value == "1"
        || value.Equals("yes", StringComparison.OrdinalIgnoreCase);

    private static void ApplyLegacyCycleChange(SeasonCycle cycle, CrewmateAidStatChangeItem item)
    {
        if (item.Field == CrewmateAidStatField.CycleReceived)
        {
            cycle.CycleReceived = decimal.Parse(item.NewValue, CultureInfo.InvariantCulture);
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

                if (threshold.Order < 1)
                {
                    error = "Survival threshold order must be 1 or greater.";
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

        if (field is CrewmateAidStatField.InNeedOfAid or CrewmateAidStatField.NeedsSurvivalAid)
        {
            if (!TryParseBool(trimmed, out var flag))
            {
                error = $"{FormatFieldLabel(field)} must be true or false.";
                return false;
            }

            normalized = flag ? "true" : "false";
            return true;
        }

        if (field is CrewmateAidStatField.EmergencyLevel or CrewmateAidStatField.DisabilityLevel)
        {
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var level)
                && !int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out level))
            {
                error = $"{FormatFieldLabel(field)} must be a whole number.";
                return false;
            }

            if (level is < 0 or > 3)
            {
                error = $"{FormatFieldLabel(field)} must be between 0 and 3.";
                return false;
            }

            normalized = level.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (field == CrewmateAidStatField.PeopleRepresentedCount)
        {
            if (!int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.InvariantCulture, out var people)
                && !int.TryParse(trimmed, NumberStyles.Integer, CultureInfo.CurrentCulture, out people))
            {
                error = "People represented must be a whole number.";
                return false;
            }

            if (people is < 1 or > 99)
            {
                error = "People represented must be between 1 and 99.";
                return false;
            }

            normalized = people.ToString(CultureInfo.InvariantCulture);
            return true;
        }

        if (field == CrewmateAidStatField.PaymentPlatforms)
        {
            return TryNormalizePaymentPlatforms(trimmed, out normalized, out error);
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

    private static bool TryNormalizePaymentPlatforms(
        string raw,
        out string normalized,
        out string error)
    {
        normalized = string.Empty;
        error = string.Empty;

        List<AidPaymentPlatformDraftDto>? drafts;
        try
        {
            drafts = JsonSerializer.Deserialize<List<AidPaymentPlatformDraftDto>>(raw, JsonOptions);
        }
        catch (JsonException)
        {
            error = "Payment platforms payload is invalid.";
            return false;
        }

        if (drafts is null)
        {
            error = "Payment platforms payload is invalid.";
            return false;
        }

        var cleaned = new List<AidPaymentPlatformDraftDto>();
        var preferredAssigned = false;
        foreach (var draft in drafts)
        {
            var handle = (draft.Handle ?? string.Empty).Trim();
            var customName = draft.CustomPlatformName?.Trim();
            if (string.IsNullOrWhiteSpace(handle))
            {
                continue;
            }

            if (draft.PlatformId <= 0 && string.IsNullOrWhiteSpace(customName))
            {
                error = "Each payment platform needs a platform id or custom name.";
                return false;
            }

            var isPreferred = draft.IsPreferred && !preferredAssigned;
            if (isPreferred)
            {
                preferredAssigned = true;
            }

            cleaned.Add(new AidPaymentPlatformDraftDto
            {
                PlatformId = draft.PlatformId,
                PlatformName = string.IsNullOrWhiteSpace(draft.PlatformName) ? null : draft.PlatformName.Trim(),
                CustomPlatformName = string.IsNullOrWhiteSpace(customName) ? null : customName,
                Handle = handle,
                IsPreferred = isPreferred
            });
        }

        if (!preferredAssigned && cleaned.Count > 0)
        {
            cleaned[0].IsPreferred = true;
        }

        normalized = JsonSerializer.Serialize(cleaned, JsonOptions);
        return true;
    }

    private static bool TryParseBool(string raw, out bool value)
    {
        value = false;
        if (raw.Equals("true", StringComparison.OrdinalIgnoreCase)
            || raw == "1"
            || raw.Equals("yes", StringComparison.OrdinalIgnoreCase))
        {
            value = true;
            return true;
        }

        if (raw.Equals("false", StringComparison.OrdinalIgnoreCase)
            || raw == "0"
            || raw.Equals("no", StringComparison.OrdinalIgnoreCase))
        {
            value = false;
            return true;
        }

        return false;
    }

    private async Task<CrewmateAidStatChangeDescriber.CurrentAidSnapshot> BuildCurrentAidSnapshotAsync(
        int crewId,
        int targetUserId,
        CrewMembership membership,
        CancellationToken cancellationToken)
    {
        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        var giftStats = await giftRepository.GetCrewmateGiftStatsAsync(
            targetUserId,
            crewId,
            crew?.CurrentSeasonStartDate,
            cancellationToken);
        var targetUser = await userRepository.GetByIdWithProfileAsync(targetUserId, cancellationToken);

        AidSeasonAccountingDto? seasonAccounting = null;
        if (crew?.CurrentSeasonStartDate is DateTime seasonStart)
        {
            var cycle = await mutualAidRepository.GetPrimarySeasonCycleAsync(
                crewId,
                targetUserId,
                seasonStart,
                cancellationToken);
            var thresholds = await mutualAidRepository.GetThresholdsForUserAsync(
                crewId,
                targetUserId,
                cancellationToken);
            var isFinancialMember = await mutualAidService.IsFinancialMemberAsync(
                targetUserId,
                crewId,
                membership,
                cancellationToken);
            var seasonCap = isFinancialMember ? crew.SeasonMemberCycleCap : crew.SeasonNonMemberCycleCap;
            seasonAccounting = AidStatDraftSerializer.FromLiveState(
                cycle,
                thresholds,
                membership.AutoJoinSeasonOnStart,
                effectiveCycleCap: seasonCap > 0m ? seasonCap : null);
        }
        else
        {
            seasonAccounting = AidStatDraftSerializer.Deserialize(membership.AidStatDraftJson)
                ?? AidStatDraftSerializer.FromLiveState(
                    null,
                    [],
                    membership.AutoJoinSeasonOnStart);
        }

        var platformsJson = SerializeCurrentPaymentPlatforms(targetUser);
        return new CrewmateAidStatChangeDescriber.CurrentAidSnapshot
        {
            EstimatedMonthlyContribution = membership.EstimatedMonthlyContribution ?? 0m,
            LifetimeContributions = giftStats.LifetimeContributions,
            ReceptionThisYear = giftStats.ReceptionThisYear,
            PercentBoost = membership.PercentBonus,
            SeasonAccounting = seasonAccounting,
            InNeedOfAid = targetUser?.InNeedOfAid ?? true,
            NeedsSurvivalAid = targetUser?.NeedsSurvivalAid ?? false,
            EmergencyLevel = targetUser?.EmergencyLevel ?? 0,
            DisabilityLevel = targetUser?.DisabilityLevel ?? 0,
            PeopleRepresentedCount = targetUser?.PeopleRepresentedCount ?? 1,
            PaymentPlatformsJson = platformsJson
        };
    }

    private static string SerializeCurrentPaymentPlatforms(User? user)
    {
        if (user is null)
        {
            return "[]";
        }

        var drafts = user.PaymentPlatforms
            .Where(p => p.CrewPaymentPlatformId.HasValue
                && (p.CrewPaymentPlatform is null || !p.CrewPaymentPlatform.IsLibraryOfThings))
            .Select(p => new AidPaymentPlatformDraftDto
            {
                PlatformId = p.CrewPaymentPlatformId!.Value,
                PlatformName = p.CrewPaymentPlatform?.Name ?? p.PlatformName,
                Handle = p.Handle,
                IsPreferred = p.IsPreferred
            })
            .ToList();
        return JsonSerializer.Serialize(drafts, JsonOptions);
    }

    private static string FormatFieldLabel(CrewmateAidStatField field) =>
        CrewmateAidStatChangeDescriber.FormatFieldLabel(field);

    private static string AppendApplyNote(string description, string note) =>
        ProposalMapper.Clip($"{description}\n({note})", ProposalMapper.AidDescriptionMaxLength);
}
