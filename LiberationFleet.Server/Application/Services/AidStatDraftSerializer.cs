using System.Text.Json;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public static class AidStatDraftSerializer
{
    public static readonly JsonSerializerOptions JsonOptions = new()
    {
        PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
        WriteIndented = false
    };

    public static string Serialize(AidSeasonAccountingDto draft) =>
        JsonSerializer.Serialize(draft, JsonOptions);

    public static AidSeasonAccountingDto? Deserialize(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<AidSeasonAccountingDto>(json, JsonOptions);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    public static AidSeasonAccountingDto FromLiveState(
        SeasonCycle? cycle,
        IReadOnlyList<MonthlySurvivalThreshold> thresholds,
        bool autoJoinSeasonOnStart,
        decimal? effectiveCycleCap = null)
    {
        var thresholdDtos = thresholds
            .OrderBy(t => t.ReceptionOrderPosition)
            .Select(t => new AidSurvivalThresholdDraftDto
            {
                Id = t.Id,
                ThresholdAmount = t.ThresholdAmount,
                AmountRemaining = Math.Max(0m, t.ThresholdAmount - t.ReceivedAmount),
                // Crew-wide 1-based order (not local index among this crewmate's rows).
                Order = t.ReceptionOrderPosition + 1
            })
            .ToList();

        decimal? cycleCap = null;
        if (cycle is not null)
        {
            if (cycle.UsesSegmentCap || cycle.EmergencyRequestId.HasValue || cycle.EmergencySplitOfferId.HasValue)
            {
                cycleCap = cycle.CycleCapAtStart;
            }
            else
            {
                cycleCap = effectiveCycleCap ?? cycle.CycleCapAtStart;
            }
        }

        return new AidSeasonAccountingDto
        {
            CycleReceived = cycle?.CycleReceived ?? 0m,
            CycleCap = cycleCap,
            HasActiveCycle = cycle?.HasCycleStarted == true,
            ReceptionOrder = cycle is null ? null : cycle.ReceptionOrderPosition + 1,
            AutoJoinSeasonOnStart = autoJoinSeasonOnStart,
            SurvivalThresholds = thresholdDtos
        };
    }
}
