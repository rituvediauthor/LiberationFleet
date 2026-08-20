using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Features.Gifts;

/// <summary>
/// Records a custom gift against a selected category, splitting overflow into an "Other" gift.
/// Example: $75 toward a $20 survival need → $20 survival gift + $55 other gift.
/// </summary>
public class CustomGiftRecordingService(
    IGiftRepository giftRepository,
    IMutualAidRepository mutualAidRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    EmergencyReconciliationService reconciliationService,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : ICustomGiftRecordingService
{
    public async Task<(Gift? AppliedGift, Gift? OtherGift)> RecordAsync(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        int paymentPlatformId,
        int? middlemanId,
        CustomGiftCategory category,
        CancellationToken cancellationToken)
    {
        if (amount <= 0m)
        {
            return (null, null);
        }

        decimal appliedAmount;
        int? emergencyRequestId = null;
        int? seasonCycleId = null;
        var isSurvival = false;

        switch (category)
        {
            case CustomGiftCategory.SurvivalThreshold:
                appliedAmount = await GetSurvivalApplyAmountAsync(crewId, recipientUserId, amount, cancellationToken);
                isSurvival = appliedAmount > 0m;
                break;
            case CustomGiftCategory.Cycle:
                (appliedAmount, seasonCycleId) = await GetCycleApplyAmountAsync(
                    crewId,
                    recipientUserId,
                    amount,
                    cancellationToken);
                break;
            case CustomGiftCategory.Emergency:
                (appliedAmount, emergencyRequestId, seasonCycleId) = await PrepareEmergencyAsync(
                    crewId,
                    recipientUserId,
                    amount,
                    cancellationToken);
                break;
            default:
                appliedAmount = 0m;
                break;
        }

        var otherAmount = Math.Max(0m, amount - appliedAmount);
        Gift? appliedGift = null;
        Gift? otherGift = null;
        var countsTowardReception = !middlemanId.HasValue;

        if (appliedAmount > 0m)
        {
            appliedGift = CreateGift(
                crewId,
                giverUserId,
                recipientUserId,
                appliedAmount,
                paymentPlatformId,
                middlemanId,
                category,
                countsTowardReception,
                isSurvivalThreshold: isSurvival,
                emergencyRequestId,
                seasonCycleId);
            await giftRepository.AddAsync(appliedGift, cancellationToken);

            if (category == CustomGiftCategory.Emergency && emergencyRequestId.HasValue)
            {
                var request = await emergencyRequestRepository.GetByIdWithDetailsAsync(
                    emergencyRequestId.Value,
                    cancellationToken);
                if (request is not null)
                {
                    await emergencyRequestRepository.AddGiftResponseAsync(new EmergencyGiftResponse
                    {
                        EmergencyRequest = request,
                        GiverUserId = giverUserId,
                        Gift = appliedGift,
                        Amount = appliedAmount,
                        CreatedAt = DateTime.UtcNow
                    }, cancellationToken);
                }

                await mutualAidService.RecordEmergencySacrificeAsync(crewId, giverUserId, cancellationToken);
            }
        }

        if (otherAmount > 0m)
        {
            otherGift = CreateGift(
                crewId,
                giverUserId,
                recipientUserId,
                otherAmount,
                paymentPlatformId,
                middlemanId,
                CustomGiftCategory.Other,
                countsTowardReception: false,
                isSurvivalThreshold: false,
                emergencyRequestId: null,
                seasonCycleId: null);
            await giftRepository.AddAsync(otherGift, cancellationToken);
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (appliedGift is not null && appliedGift.CountsTowardReception)
        {
            await mutualAidService.ApplyGiftReceptionAsync(appliedGift, cancellationToken);
        }

        return (appliedGift, otherGift);
    }

    public static CustomGiftCategory ParseCategory(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return CustomGiftCategory.Other;
        }

        return value.Trim().ToLowerInvariant() switch
        {
            "cycle" => CustomGiftCategory.Cycle,
            "survivalthreshold" or "survival" or "survival_threshold" => CustomGiftCategory.SurvivalThreshold,
            "emergency" => CustomGiftCategory.Emergency,
            "other" => CustomGiftCategory.Other,
            _ => CustomGiftCategory.Other
        };
    }

    public static string ToApiValue(CustomGiftCategory category) =>
        category switch
        {
            CustomGiftCategory.Cycle => "cycle",
            CustomGiftCategory.SurvivalThreshold => "survivalThreshold",
            CustomGiftCategory.Emergency => "emergency",
            _ => "other"
        };

    public static string ToDisplayLabel(CustomGiftCategory category) =>
        category switch
        {
            CustomGiftCategory.Cycle => "Cycle",
            CustomGiftCategory.SurvivalThreshold => "Survival threshold",
            CustomGiftCategory.Emergency => "Emergency",
            _ => "Other"
        };

    private async Task<decimal> GetSurvivalApplyAmountAsync(
        int crewId,
        int recipientUserId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew is null || !crew.AllowSurvivalThresholds)
        {
            return 0m;
        }

        var need = (await mutualAidRepository.GetUnsatisfiedThresholdsAsync(crewId, cancellationToken))
            .Where(t => t.UserId == recipientUserId)
            .Sum(t => Math.Max(0m, t.ThresholdAmount - t.ReceivedAmount));
        return Math.Min(amount, need);
    }

    private async Task<(decimal Applied, int? SeasonCycleId)> GetCycleApplyAmountAsync(
        int crewId,
        int recipientUserId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        if (crew?.CurrentSeasonStartDate is null)
        {
            return (0m, null);
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
            crewId,
            crew.CurrentSeasonStartDate.Value,
            cancellationToken);
        var target = cycles
            .Where(c =>
                c.UserId == recipientUserId
                && !c.EmergencyRequestId.HasValue
                && !c.EmergencySplitOfferId.HasValue
                && !c.CycleCompleted)
            .OrderByDescending(c => c.HasCycleStarted)
            .ThenBy(c => c.ReceptionOrderPosition)
            .FirstOrDefault();

        if (target is null)
        {
            return (0m, null);
        }

        var room = Math.Max(0m, target.CycleCapAtStart - target.CycleReceived);
        return (Math.Min(amount, room), target.Id);
    }

    private async Task<(decimal Applied, int? EmergencyRequestId, int? SeasonCycleId)> PrepareEmergencyAsync(
        int crewId,
        int recipientUserId,
        decimal amount,
        CancellationToken cancellationToken)
    {
        var open = await emergencyRequestRepository.GetOpenByCrewIdAsync(crewId, cancellationToken);
        var request = open
            .Where(r => r.RequesterUserId == recipientUserId)
            .OrderBy(r => r.CreatedAt)
            .FirstOrDefault();
        if (request is null)
        {
            return (0m, null, null);
        }

        // Load with details for split shrink during reconciliation.
        var detailed = await emergencyRequestRepository.GetByIdWithDetailsAsync(request.Id, cancellationToken)
            ?? request;
        var reconciliation = await reconciliationService.ApplyDirectGiftAsync(
            detailed,
            amount,
            cancellationToken);
        if (reconciliation.AmountAppliedToNeed <= 0m)
        {
            return (0m, null, null);
        }

        var crew = await mutualAidRepository.GetCrewAsync(crewId, cancellationToken);
        int? seasonCycleId = null;
        if (crew?.CurrentSeasonStartDate is not null)
        {
            var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
                crewId,
                crew.CurrentSeasonStartDate.Value,
                cancellationToken);
            seasonCycleId = cycles
                .Where(c => c.EmergencyRequestId == detailed.Id && !c.CycleCompleted)
                .OrderBy(c => c.ReceptionOrderPosition)
                .FirstOrDefault()?.Id;
        }

        return (reconciliation.AmountAppliedToNeed, detailed.Id, seasonCycleId);
    }

    private static Gift CreateGift(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        int paymentPlatformId,
        int? middlemanId,
        CustomGiftCategory category,
        bool countsTowardReception,
        bool isSurvivalThreshold,
        int? emergencyRequestId,
        int? seasonCycleId) =>
        new()
        {
            CrewId = crewId,
            GiverUserId = giverUserId,
            RecipientUserId = recipientUserId,
            MiddlemanUserId = middlemanId,
            Type = middlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatformId = paymentPlatformId,
            IsCustomGift = true,
            CustomGiftCategory = category,
            IsSurvivalThreshold = isSurvivalThreshold,
            CountsTowardReception = countsTowardReception,
            CountsTowardContribution = true,
            EmergencyRequestId = emergencyRequestId,
            SeasonCycleId = seasonCycleId,
            VerificationStatus = GiftVerificationStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };
}
