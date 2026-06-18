using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IMutualAidRepository
{
    Task<Crew?> GetCrewAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetSeasonParticipantsAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetSeasonReadyMembersAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetActiveMembersWithUsersAsync(int crewId, CancellationToken cancellationToken = default);
    Task<SeasonCycle?> GetSeasonCycleAsync(int crewId, int userId, DateTime seasonStartDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeasonCycle>> GetSeasonCyclesAsync(int crewId, DateTime seasonStartDate, CancellationToken cancellationToken = default);
    Task AddSeasonCycleAsync(SeasonCycle cycle, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlySurvivalThreshold>> GetUnsatisfiedThresholdsAsync(int crewId, CancellationToken cancellationToken = default);
    Task<MonthlySurvivalThreshold?> GetThresholdByIdAsync(int thresholdId, CancellationToken cancellationToken = default);
    Task AddThresholdAsync(MonthlySurvivalThreshold threshold, CancellationToken cancellationToken = default);
    Task<bool> HasThresholdForMonthAsync(int crewId, int userId, int year, int month, CancellationToken cancellationToken = default);
    Task<decimal> GetContributionsLast3MonthsAsync(int userId, int crewId, CancellationToken cancellationToken = default);
    Task<decimal> GetLifetimeContributionsAsync(int userId, int crewId, DateTime? before = null, CancellationToken cancellationToken = default);
    Task<decimal> GetCrewLifetimeContributionsAsync(int crewId, DateTime? before = null, CancellationToken cancellationToken = default);
    Task<bool> HasContributedSinceAsync(int userId, int crewId, DateTime since, DateTime? until = null, CancellationToken cancellationToken = default);
    Task<DateTime?> GetPreviousSeasonStartDateAsync(int crewId, DateTime currentSeasonStart, CancellationToken cancellationToken = default);
    Task<int> GetNextThresholdOrderPositionAsync(int crewId, CancellationToken cancellationToken = default);
}
