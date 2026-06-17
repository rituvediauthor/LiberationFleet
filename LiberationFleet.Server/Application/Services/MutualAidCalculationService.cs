using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Application.Services;

public interface IMutualAidCalculationService
{
    Task<decimal> CalculateTotalMonthlyGivingCapacityAsync(int crewId);
    Task<decimal> CalculateSurvivalThresholdAmountAsync(int crewId);
    Task<decimal> CalculateCycleCapForMemberAsync(int crewId, bool isMember);
    Task<decimal> CalculatePriorityScoreAsync(int userId, int crewId);
    Task<bool> IsMemberAsync(int userId, int crewId);
    Task<decimal> GetUserLifetimeContributionsAsync(int userId, int crewId);
    Task<decimal> GetCrewLifetimeContributionsAsync(int crewId);
    Task<decimal> GetUserMonthlyGivingCapacityAsync(int userId, int crewId);
}

public class MutualAidCalculationService : IMutualAidCalculationService
{
    private readonly IUnitOfWork _unitOfWork;

    public MutualAidCalculationService(IUnitOfWork unitOfWork)
    {
        _unitOfWork = unitOfWork;
    }

    public async Task<decimal> CalculateTotalMonthlyGivingCapacityAsync(int crewId)
    {
        var crewMemberIds = await _unitOfWork.Set<CrewMembership>()
            .Where(cm => cm.CrewId == crewId && !cm.IsBanned)
            .Select(cm => cm.UserId)
            .ToListAsync();

        decimal totalCapacity = 0;
        foreach (var userId in crewMemberIds)
        {
            totalCapacity += await GetUserMonthlyGivingCapacityAsync(userId, crewId);
        }

        return totalCapacity;
    }

    public async Task<decimal> GetUserMonthlyGivingCapacityAsync(int userId, int crewId)
    {
        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-3);

        var contributionsLast3Months = await _unitOfWork.Set<Gift>()
            .Where(g => g.CrewId == crewId 
                && g.GiverUserId == userId 
                && g.CreatedAt >= threeMonthsAgo
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed))
            .SumAsync(g => (decimal?)g.Amount) ?? 0;

        return contributionsLast3Months / 3;
    }

    public async Task<decimal> CalculateSurvivalThresholdAmountAsync(int crewId)
    {
        var totalMonthlyCapacity = await CalculateTotalMonthlyGivingCapacityAsync(crewId);

        var survivorCount = await _unitOfWork.Set<User>()
            .Where(u => u.NeedsSurvivalAid 
                && u.CrewMemberships.Any(cm => cm.CrewId == crewId && !cm.IsBanned))
            .CountAsync();

        if (survivorCount == 0)
            return 0;

        return (totalMonthlyCapacity / 2) / survivorCount;
    }

    public async Task<decimal> CalculateCycleCapForMemberAsync(int crewId, bool isMember)
    {
        var totalMonthlyCapacity = await CalculateTotalMonthlyGivingCapacityAsync(crewId);

        if (isMember)
            return totalMonthlyCapacity * 2;
        else
            return totalMonthlyCapacity / 2;
    }

    public async Task<bool> IsMemberAsync(int userId, int crewId)
    {
        var membership = await _unitOfWork.Set<CrewMembership>()
            .FirstOrDefaultAsync(cm => cm.UserId == userId && cm.CrewId == crewId);

        if (membership == null)
            return false;

        if (membership.IsHonoraryMember)
            return true;

        var crew = await _unitOfWork.Set<Crew>().FindAsync(crewId);
        if (crew == null)
            return false;

        var currentSeasonStart = crew.CurrentSeasonStartDate;
        var previousSeasonStart = currentSeasonStart.AddYears(-1);

        var hasContributedThisSeason = await _unitOfWork.Set<Gift>()
            .AnyAsync(g => g.CrewId == crewId 
                && g.GiverUserId == userId
                && g.CreatedAt >= currentSeasonStart
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        if (hasContributedThisSeason)
            return true;

        var hasContributedLastSeason = await _unitOfWork.Set<Gift>()
            .AnyAsync(g => g.CrewId == crewId 
                && g.GiverUserId == userId
                && g.CreatedAt >= previousSeasonStart
                && g.CreatedAt < currentSeasonStart
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed));

        return hasContributedLastSeason;
    }

    public async Task<decimal> GetUserLifetimeContributionsAsync(int userId, int crewId)
    {
        return await _unitOfWork.Set<Gift>()
            .Where(g => g.CrewId == crewId 
                && g.GiverUserId == userId
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed))
            .SumAsync(g => (decimal?)g.Amount) ?? 0;
    }

    public async Task<decimal> GetCrewLifetimeContributionsAsync(int crewId)
    {
        return await _unitOfWork.Set<Gift>()
            .Where(g => g.CrewId == crewId
                && (g.Type == GiftType.Direct || g.Type == GiftType.Completed))
            .SumAsync(g => (decimal?)g.Amount) ?? 0;
    }

    public async Task<decimal> CalculatePriorityScoreAsync(int userId, int crewId)
    {
        var membership = await _unitOfWork.Set<CrewMembership>()
            .FirstOrDefaultAsync(cm => cm.UserId == userId && cm.CrewId == crewId);

        if (membership == null)
            return -3;

        if (membership.IsOrganizer)
            return -1;

        var user = await _unitOfWork.Set<User>().FindAsync(userId);
        if (user == null || !user.InNeedOfAid)
            return -2;

        var totalCrewLifetimeContributions = await GetCrewLifetimeContributionsAsync(crewId);
        var userLifetimeContributions = await GetUserLifetimeContributionsAsync(userId, crewId);
        var survivalThresholdAmount = await CalculateSurvivalThresholdAmountAsync(crewId);
        var isMember = await IsMemberAsync(userId, crewId);
        var membershipBonus = isMember ? 1 : 0;

        var score = (totalCrewLifetimeContributions * user.EmergencyLevel)
            + membershipBonus
            + userLifetimeContributions
            + (survivalThresholdAmount * (1 - (user.PercentBonus / 100m)));

        return score;
    }
}
