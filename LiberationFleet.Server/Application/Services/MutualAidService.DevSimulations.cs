using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Services;

public partial class MutualAidService
{
    public async Task<DevActionResultDto> SimulateNewMonthAsync(int userId, CancellationToken cancellationToken = default)
    {
        var crew = await GetCrewForUserAsync(userId, cancellationToken);
        if (crew is null)
        {
            return DevFail("You are not in a crew.");
        }

        if (!crew.SeasonStarted)
        {
            return DevFail("Season has not started.");
        }

        if (!crew.AllowSurvivalThresholds)
        {
            return DevSuccess("Survival thresholds are disabled for this crew.");
        }

        var (year, month) = await GetNextSimulationMonthAsync(crew.Id, cancellationToken);
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);

        if (capacityContext.SurvivalThresholdAmount <= 0)
        {
            return DevSuccess("No crewmates need survival aid this season.");
        }

        var survivalRecipients = participants.Count(m => m.User.NeedsSurvivalAid);
        if (survivalRecipients == 0)
        {
            return DevSuccess("No crewmates need survival aid this season.");
        }

        var existingCount = 0;
        foreach (var member in participants.Where(m => m.User.NeedsSurvivalAid))
        {
            if (await mutualAidRepository.HasThresholdForMonthAsync(crew.Id, member.UserId, year, month, cancellationToken))
            {
                existingCount++;
            }
        }

        if (existingCount == survivalRecipients)
        {
            return DevSuccess($"Survival thresholds for {year}-{month:D2} already exist.");
        }

        await CreateMonthlyThresholdsForMonthAsync(crew, year, month, cancellationToken);
        return DevSuccess($"Created survival threshold(s) for {year}-{month:D2}.");
    }

    public async Task<DevActionResultDto> SimulateNewSeasonAsync(int userId, CancellationToken cancellationToken = default)
    {
        var crew = await GetCrewForUserAsync(userId, cancellationToken);
        if (crew is null)
        {
            return DevFail("You are not in a crew.");
        }

        if (!crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return DevFail("Season has not started.");
        }

        crew.CurrentSeasonStartDate = DateTime.UtcNow;
        var participants = await mutualAidRepository.GetSeasonParticipantsAsync(crew.Id, cancellationToken);
        await InitializeSeasonStateAsync(crew, participants, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DevSuccess("Started a new season.");
    }

    public async Task<DevActionResultDto> CompleteAllCyclesAsync(int userId, CancellationToken cancellationToken = default)
    {
        var crew = await GetCrewForUserAsync(userId, cancellationToken);
        if (crew is null)
        {
            return DevFail("You are not in a crew.");
        }

        if (!crew.SeasonStarted || !crew.CurrentSeasonStartDate.HasValue)
        {
            return DevFail("Season has not started.");
        }

        var cycles = await mutualAidRepository.GetSeasonCyclesAsync(crew.Id, crew.CurrentSeasonStartDate.Value, cancellationToken);
        var capacityContext = await BuildCapacityContextAsync(crew, cancellationToken);

        foreach (var cycle in cycles)
        {
            var cap = await GetEffectiveCycleCapForUserAsync(cycle.UserId, crew, capacityContext, cancellationToken);
            cycle.HasCycleStarted = true;
            cycle.CycleReceived = cap;
            cycle.TotalReceptionAmount = Math.Max(cycle.TotalReceptionAmount, cap);
            cycle.CycleCompleted = true;
            cycle.CycleCompletedAt = DateTime.UtcNow;
        }

        await TryEndSeasonAsync(crew, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return DevSuccess("Marked all cycles complete. Season rollover ran if applicable.");
    }

    public async Task<DevActionResultDto> ResetSeasonAsync(int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return DevFail("You are not in a crew.");
        }

        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return DevFail("Crew not found.");
        }

        crew.SeasonStarted = false;
        crew.CurrentSeasonStartDate = null;
        crew.SeasonMemberCycleCap = 0m;
        crew.SeasonNonMemberCycleCap = 0m;

        var members = await mutualAidRepository.GetActiveMembersWithUsersAsync(crew.Id, cancellationToken);
        foreach (var member in members)
        {
            member.IsInSeason = false;
            member.IsSeasonReady = false;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);
        return DevSuccess("Season reset. Ready states cleared.");
    }

    public async Task<DevActionResultDto> RecalculateCapsAsync(int userId, CancellationToken cancellationToken = default)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return DevFail("You are not in a crew.");
        }

        await RecalculateCapsAfterMembershipChangeAsync(membership.CrewId, cancellationToken);
        return DevSuccess("Recalculated cycle caps.");
    }

    private async Task<Crew?> GetCrewForUserAsync(int userId, CancellationToken cancellationToken)
    {
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return null;
        }

        return await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
    }

    private async Task<(int Year, int Month)> GetNextSimulationMonthAsync(int crewId, CancellationToken cancellationToken)
    {
        var latest = await mutualAidRepository.GetLatestThresholdMonthAsync(crewId, cancellationToken);
        if (latest is null)
        {
            var now = DateTime.UtcNow;
            return (now.Year, now.Month);
        }

        var next = new DateTime(latest.Value.Year, latest.Value.Month, 1, 0, 0, 0, DateTimeKind.Utc).AddMonths(1);
        return (next.Year, next.Month);
    }

    private static DevActionResultDto DevSuccess(string message) =>
        new() { Success = true, Message = message };

    private static DevActionResultDto DevFail(string message) =>
        new() { Success = false, Message = message };
}