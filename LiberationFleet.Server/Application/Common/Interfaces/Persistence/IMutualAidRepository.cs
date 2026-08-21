using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common.Interfaces.Persistence;

public interface IMutualAidRepository
{
    Task<Crew?> GetCrewAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetSeasonParticipantsAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetSeasonReadyMembersAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<CrewMembership>> GetActiveMembersWithUsersAsync(int crewId, CancellationToken cancellationToken = default);
    Task<SeasonCycle?> GetSeasonCycleAsync(int crewId, int userId, DateTime seasonStartDate, CancellationToken cancellationToken = default);
    /// <summary>
    /// Primary cycle only (no emergency request / split-offer binding), ordered by reception position.
    /// </summary>
    Task<SeasonCycle?> GetPrimarySeasonCycleAsync(int crewId, int userId, DateTime seasonStartDate, CancellationToken cancellationToken = default);
    Task<SeasonCycle?> GetSeasonCycleByIdAsync(int cycleId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<SeasonCycle>> GetSeasonCyclesAsync(int crewId, DateTime seasonStartDate, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<DateTime>> GetSeasonStartDatesOnOrAfterAsync(int crewId, DateTime onOrAfter, CancellationToken cancellationToken = default);
    Task AddSeasonCycleAsync(SeasonCycle cycle, CancellationToken cancellationToken = default);
    /// <summary>
    /// Removes season cycles, monthly survival thresholds, and crew emergency requests/splits
    /// for a full dev season reset. Clears <c>Gift.SeasonCycleId</c> FKs first.
    /// </summary>
    Task ClearSeasonDataAsync(int crewId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<MonthlySurvivalThreshold>> GetUnsatisfiedThresholdsAsync(int crewId, CancellationToken cancellationToken = default);
    Task<MonthlySurvivalThreshold?> GetThresholdByIdAsync(int thresholdId, CancellationToken cancellationToken = default);
    Task AddThresholdAsync(MonthlySurvivalThreshold threshold, CancellationToken cancellationToken = default);
    Task<bool> HasThresholdForMonthAsync(int crewId, int userId, int year, int month, CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<(int Year, int Month), decimal>> GetFinancialContributionsByMonthAsync(
        int userId,
        int crewId,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        CancellationToken cancellationToken = default);
    Task<IReadOnlyDictionary<(int Year, int Month), decimal>> GetContributionsByMonthAsync(
        int userId,
        int crewId,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        bool includeLibraryOfThings,
        DateTime? createdBeforeUtc = null,
        CancellationToken cancellationToken = default);
    /// <summary>
    /// One query for all givers in a crew over a date range (avoids N+1 on capacity / membership checks).
    /// </summary>
    Task<IReadOnlyDictionary<int, IReadOnlyDictionary<(int Year, int Month), decimal>>> GetContributionsByMonthForCrewAsync(
        int crewId,
        DateTime rangeStartUtc,
        DateTime rangeEndExclusiveUtc,
        bool includeLibraryOfThings,
        DateTime? createdBeforeUtc = null,
        CancellationToken cancellationToken = default);
    Task<decimal> GetLifetimeContributionsAsync(int userId, int crewId, DateTime? before = null, CancellationToken cancellationToken = default);
    Task<decimal> GetCrewLifetimeContributionsAsync(int crewId, DateTime? before = null, CancellationToken cancellationToken = default);
    Task<bool> HasContributedSinceAsync(int userId, int crewId, DateTime since, DateTime? until = null, CancellationToken cancellationToken = default);
    Task<DateTime?> GetPreviousSeasonStartDateAsync(int crewId, DateTime currentSeasonStart, CancellationToken cancellationToken = default);
    Task<int> GetNextThresholdOrderPositionAsync(int crewId, CancellationToken cancellationToken = default);
    Task<(int Year, int Month)?> GetLatestThresholdMonthAsync(int crewId, CancellationToken cancellationToken = default);
    Task MergePlaceholderIdentityDataAsync(
        int crewId,
        int placeholderUserId,
        int claimantUserId,
        CancellationToken cancellationToken = default);
}
