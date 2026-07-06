using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;

namespace LiberationFleet.Server.Application.Common.Interfaces;

public interface IMutualAidService
{
    Task<SeasonStatusDto> GetSeasonStatusAsync(int userId, CancellationToken cancellationToken = default);
    Task<IReadOnlyList<ReceptionOrderEntryDto>> GetReceptionOrderAsync(
        int userId,
        int limit = 30,
        bool requireGiverInSeason = true,
        bool excludeSelfAsRecipient = true,
        CancellationToken cancellationToken = default);
    Task<NextAidDto?> GetNextAidAsync(int userId, CancellationToken cancellationToken = default);
    Task<SeasonReadyResultDto> MarkSeasonReadyAsync(int userId, CancellationToken cancellationToken = default);
    Task<SeasonSetupSaveResultDto> SaveSeasonSetupAsync(int userId, decimal estimatedMonthlyContribution, CancellationToken cancellationToken = default);
    Task<SeasonSetupSaveResultDto> ClearSeasonReadyAsync(int userId, CancellationToken cancellationToken = default);
    Task ApplyGiftReceptionAsync(Gift gift, CancellationToken cancellationToken = default);
    Task ApplyGiftReceptionForUserAsync(Gift gift, int recipientUserId, CancellationToken cancellationToken = default);
    Task OnCrewmatePriorityChangedAsync(int userId, CancellationToken cancellationToken = default);
    Task RecalculateCapsAfterMembershipChangeAsync(int crewId, CancellationToken cancellationToken = default);
    Task<decimal> GetPriorityScoreForUserAsync(
        int userId,
        int crewId,
        CancellationToken cancellationToken = default,
        bool excludeActiveSeasonContributions = false);
    Task<decimal> GetCrewMonthlyGivingCapacityAsync(int crewId, CancellationToken cancellationToken = default);
    Task<bool> IsFinancialMemberAsync(
        int userId,
        int crewId,
        CrewMembership membership,
        CancellationToken cancellationToken = default,
        bool excludeActiveSeasonContributions = false);
    Task EnsureMemberInActiveSeasonAsync(
        int crewId,
        CrewMembership membership,
        CancellationToken cancellationToken = default);
    IReadOnlyList<int> FindMiddlemen(int giverUserId, int recipientUserId, IReadOnlyList<CrewMemberPlatforms> members);
}

public class SeasonStatusDto
{
    public bool SeasonStarted { get; set; }
    public bool UserInSeason { get; set; }
    public bool UserSeasonReady { get; set; }
    public int ReadyCount { get; set; }
    public bool CanStartSeason { get; set; }
    public decimal? EstimatedMonthlyContribution { get; set; }
}

public class SeasonSetupSaveResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public SeasonStatusDto? Status { get; set; }
}

public class SeasonReadyResultDto
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public bool SeasonStarted { get; set; }
    public SeasonStatusDto? Status { get; set; }
}

public class ReceptionOrderEntryDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    public string EntryType { get; set; } = string.Empty;
    public int? ThresholdId { get; set; }
    public int? CycleUserId { get; set; }
    public int? SeasonCycleId { get; set; }
    public IReadOnlyList<MiddlemanOptionDto> MiddlemanOptions { get; set; } = Array.Empty<MiddlemanOptionDto>();
    public int? DefaultMiddlemanId { get; set; }
    public bool NoSuitableMiddleman { get; set; }
    public IReadOnlyList<int> GiverPlatformIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<int> RecipientPlatformIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<int> CommonPlatformIds { get; set; } = Array.Empty<int>();
    public string? RecipientPreferredPlatformName { get; set; }
    public string? RecipientPreferredPlatformHandle { get; set; }
    public IReadOnlyList<PlatformAccountDto> RecipientPlatformAccounts { get; set; } = Array.Empty<PlatformAccountDto>();
}

public class PlatformAccountDto
{
    public int PlatformId { get; set; }
    public string Name { get; set; } = string.Empty;
    public string Handle { get; set; } = string.Empty;
}

public class MiddlemanOptionDto
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public IReadOnlyList<int> CommonPlatformIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<PlatformAccountDto> PlatformAccounts { get; set; } = Array.Empty<PlatformAccountDto>();
}

public class NextAidDto
{
    public string RecipientName { get; set; } = string.Empty;
    public decimal Amount { get; set; }
    public bool IsCurrentUserRecipient { get; set; }
    public string PlatformDisplayKind { get; set; } = NextAidPlatformDisplayKind.None;
    public string? PlatformName { get; set; }
    public string? PlatformHandle { get; set; }
}

public static class NextAidPlatformDisplayKind
{
    public const string None = "none";
    public const string Preferred = "preferred";
    public const string Common = "common";
    public const string MiddlemanNeeded = "middlemanNeeded";
    public const string Unavailable = "unavailable";
}

public class CrewMemberPlatforms
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public IReadOnlyList<int> PlatformIds { get; set; } = Array.Empty<int>();
    public IReadOnlyList<PlatformAccountDto> PlatformAccounts { get; set; } = Array.Empty<PlatformAccountDto>();
    public int? PreferredPlatformId { get; set; }
    public string? PreferredPlatformName { get; set; }
    public string? PreferredPlatformHandle { get; set; }
}
