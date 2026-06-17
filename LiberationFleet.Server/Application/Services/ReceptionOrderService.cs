using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Application.Services;

public class RecipientNeed
{
    public int UserId { get; set; }
    public string Username { get; set; } = string.Empty;
    public decimal AmountNeeded { get; set; }
    public bool IsSurvivalThreshold { get; set; }
    public int ReceptionOrderPosition { get; set; }
    public List<int> CommonPaymentPlatforms { get; set; } = new();
    public int? SuggestedMiddlemanId { get; set; }
    public string? SuggestedMiddlemanName { get; set; }
}

public interface IReceptionOrderService
{
    Task<List<RecipientNeed>> GetOrderedRecipientsAsync(int crewId, int requestingUserId, int limit = 30);
    Task EnsureCurrentMonthThresholdsAsync(int crewId);
    Task ProcessSurvivalThresholdsForNewMonthAsync(int crewId);
    Task UpdateReceptionAmountsAsync(int giftId);
    Task CheckAndStartNewSeasonAsync(int crewId);
}

public class ReceptionOrderService : IReceptionOrderService
{
    private readonly IUnitOfWork _unitOfWork;
    private readonly IMutualAidCalculationService _calculationService;

    public ReceptionOrderService(IUnitOfWork unitOfWork, IMutualAidCalculationService calculationService)
    {
        _unitOfWork = unitOfWork;
        _calculationService = calculationService;
    }

    public async Task<List<RecipientNeed>> GetOrderedRecipientsAsync(int crewId, int requestingUserId, int limit = 30)
    {
        await EnsureCurrentMonthThresholdsAsync(crewId);

        var crew = await _unitOfWork.Set<Crew>().FindAsync(crewId);
        if (crew == null)
            return new List<RecipientNeed>();

        var currentYear = DateTime.UtcNow.Year;
        var currentMonth = DateTime.UtcNow.Month;

        var requestingUserPlatforms = await _unitOfWork.Set<UserPaymentPlatform>()
            .Where(upp => upp.UserId == requestingUserId)
            .Select(upp => upp.PaymentPlatformId)
            .ToListAsync();

        var recipientList = new List<RecipientNeed>();

        var unsatisfiedThresholds = await _unitOfWork.Set<MonthlySurvivalThreshold>()
            .Where(mst => mst.CrewId == crewId 
                && mst.Year == currentYear 
                && mst.Month == currentMonth 
                && !mst.Satisfied)
            .OrderBy(mst => mst.ReceptionOrderPosition)
            .ToListAsync();

        foreach (var threshold in unsatisfiedThresholds)
        {
            var user = await _unitOfWork.Set<User>().FindAsync(threshold.UserId);
            if (user == null) continue;

            var amountNeeded = threshold.ThresholdAmount - threshold.ReceivedAmount;
            if (amountNeeded <= 0) continue;

            var (commonPlatforms, middlemanId, middlemanName) = await FindPaymentOptionsAsync(
                requestingUserId, threshold.UserId, crewId, requestingUserPlatforms);

            recipientList.Add(new RecipientNeed
            {
                UserId = threshold.UserId,
                Username = user.Username,
                AmountNeeded = amountNeeded,
                IsSurvivalThreshold = true,
                ReceptionOrderPosition = threshold.ReceptionOrderPosition,
                CommonPaymentPlatforms = commonPlatforms,
                SuggestedMiddlemanId = middlemanId,
                SuggestedMiddlemanName = middlemanName
            });
        }

        var activeCycles = await _unitOfWork.Set<SeasonCycle>()
            .Where(sc => sc.CrewId == crewId 
                && sc.SeasonStartDate == crew.CurrentSeasonStartDate
                && !sc.CycleCompleted)
            .OrderBy(sc => sc.ReceptionOrderPosition)
            .ToListAsync();

        foreach (var cycle in activeCycles)
        {
            var user = await _unitOfWork.Set<User>().FindAsync(cycle.UserId);
            if (user == null) continue;

            var amountNeeded = cycle.CycleCapAtStart - cycle.CycleReceived;
            if (amountNeeded <= 0) continue;

            var (commonPlatforms, middlemanId, middlemanName) = await FindPaymentOptionsAsync(
                requestingUserId, cycle.UserId, crewId, requestingUserPlatforms);

            recipientList.Add(new RecipientNeed
            {
                UserId = cycle.UserId,
                Username = user.Username,
                AmountNeeded = amountNeeded,
                IsSurvivalThreshold = false,
                ReceptionOrderPosition = cycle.ReceptionOrderPosition + 10000,
                CommonPaymentPlatforms = commonPlatforms,
                SuggestedMiddlemanId = middlemanId,
                SuggestedMiddlemanName = middlemanName
            });
        }

        return recipientList
            .OrderBy(r => r.ReceptionOrderPosition)
            .Take(limit)
            .ToList();
    }

    private async Task<(List<int> commonPlatforms, int? middlemanId, string? middlemanName)> FindPaymentOptionsAsync(
        int giverId, int recipientId, int crewId, List<int> giverPlatforms)
    {
        var recipientPlatforms = await _unitOfWork.Set<UserPaymentPlatform>()
            .Where(upp => upp.UserId == recipientId)
            .Select(upp => upp.PaymentPlatformId)
            .ToListAsync();

        var commonPlatforms = giverPlatforms.Intersect(recipientPlatforms).ToList();

        if (commonPlatforms.Any())
            return (commonPlatforms, null, null);

        var crewMemberIds = await _unitOfWork.Set<CrewMembership>()
            .Where(cm => cm.CrewId == crewId && !cm.IsBanned && cm.UserId != giverId && cm.UserId != recipientId)
            .Select(cm => cm.UserId)
            .ToListAsync();

        foreach (var potentialMiddlemanId in crewMemberIds)
        {
            var middlemanPlatforms = await _unitOfWork.Set<UserPaymentPlatform>()
                .Where(upp => upp.UserId == potentialMiddlemanId)
                .Select(upp => upp.PaymentPlatformId)
                .ToListAsync();

            var sharesWithGiver = giverPlatforms.Intersect(middlemanPlatforms).Any();
            var sharesWithRecipient = recipientPlatforms.Intersect(middlemanPlatforms).Any();

            if (sharesWithGiver && sharesWithRecipient)
            {
                var middleman = await _unitOfWork.Set<User>().FindAsync(potentialMiddlemanId);
                return (new List<int>(), potentialMiddlemanId, middleman?.Username);
            }
        }

        return (new List<int>(), null, null);
    }

    public async Task EnsureCurrentMonthThresholdsAsync(int crewId)
    {
        var currentYear = DateTime.UtcNow.Year;
        var currentMonth = DateTime.UtcNow.Month;

        var existingThresholds = await _unitOfWork.Set<MonthlySurvivalThreshold>()
            .AnyAsync(mst => mst.CrewId == crewId && mst.Year == currentYear && mst.Month == currentMonth);

        if (existingThresholds)
            return;

        await ProcessSurvivalThresholdsForNewMonthAsync(crewId);
    }

    public async Task ProcessSurvivalThresholdsForNewMonthAsync(int crewId)
    {
        var currentYear = DateTime.UtcNow.Year;
        var currentMonth = DateTime.UtcNow.Month;

        var thresholdAmount = await _calculationService.CalculateSurvivalThresholdAmountAsync(crewId);

        var survivors = await _unitOfWork.Set<User>()
            .Where(u => u.NeedsSurvivalAid 
                && u.CrewMemberships.Any(cm => cm.CrewId == crewId && !cm.IsBanned))
            .ToListAsync();

        var existingUnsatisfied = await _unitOfWork.Set<MonthlySurvivalThreshold>()
            .Where(mst => mst.CrewId == crewId && !mst.Satisfied)
            .OrderBy(mst => mst.ReceptionOrderPosition)
            .ToListAsync();

        int nextPosition = existingUnsatisfied.Any() ? existingUnsatisfied.Max(e => e.ReceptionOrderPosition) + 1 : 0;

        var scoredSurvivors = new List<(User user, decimal score)>();
        foreach (var survivor in survivors)
        {
            var score = await _calculationService.CalculatePriorityScoreAsync(survivor.Id, crewId);
            scoredSurvivors.Add((survivor, score));
        }

        var orderedSurvivors = scoredSurvivors
            .OrderByDescending(s => s.score)
            .ToList();

        foreach (var (survivor, _) in orderedSurvivors)
        {
            var threshold = new MonthlySurvivalThreshold
            {
                CrewId = crewId,
                UserId = survivor.Id,
                Year = currentYear,
                Month = currentMonth,
                ThresholdAmount = thresholdAmount,
                ReceivedAmount = 0,
                ReceptionOrderPosition = nextPosition++,
                Satisfied = false
            };

            await _unitOfWork.Set<MonthlySurvivalThreshold>().AddAsync(threshold);
        }

        await _unitOfWork.SaveChangesAsync();
    }

    public async Task UpdateReceptionAmountsAsync(int giftId)
    {
        var gift = await _unitOfWork.Set<Gift>().FindAsync(giftId);
        if (gift == null || !gift.CountsTowardReception)
            return;

        var crew = await _unitOfWork.Set<Crew>().FindAsync(gift.CrewId);
        if (crew == null)
            return;

        if (gift.IsSurvivalThreshold)
        {
            var currentYear = DateTime.UtcNow.Year;
            var currentMonth = DateTime.UtcNow.Month;

            var threshold = await _unitOfWork.Set<MonthlySurvivalThreshold>()
                .FirstOrDefaultAsync(mst => mst.CrewId == gift.CrewId 
                    && mst.UserId == gift.RecipientUserId
                    && mst.Year == currentYear
                    && mst.Month == currentMonth);

            if (threshold != null)
            {
                threshold.ReceivedAmount += gift.Amount;
                if (threshold.ReceivedAmount >= threshold.ThresholdAmount)
                    threshold.Satisfied = true;
            }
        }

        var cycle = await _unitOfWork.Set<SeasonCycle>()
            .FirstOrDefaultAsync(sc => sc.CrewId == gift.CrewId 
                && sc.UserId == gift.RecipientUserId
                && sc.SeasonStartDate == crew.CurrentSeasonStartDate);

        if (cycle != null)
        {
            cycle.TotalReceptionAmount += gift.Amount;
            
            if (gift.IsSurvivalThreshold)
                cycle.SurvivalThresholdReceived += gift.Amount;
            else
                cycle.CycleReceived += gift.Amount;

            if (cycle.CycleReceived >= cycle.CycleCapAtStart && !cycle.CycleCompleted)
            {
                cycle.CycleCompleted = true;
                cycle.CycleCompletedAt = DateTime.UtcNow;
            }
        }

        await _unitOfWork.SaveChangesAsync();

        await CheckAndStartNewSeasonAsync(gift.CrewId);
    }

    public async Task CheckAndStartNewSeasonAsync(int crewId)
    {
        var crew = await _unitOfWork.Set<Crew>().FindAsync(crewId);
        if (crew == null)
            return;

        var allCyclesCompleted = await _unitOfWork.Set<SeasonCycle>()
            .Where(sc => sc.CrewId == crewId && sc.SeasonStartDate == crew.CurrentSeasonStartDate)
            .AllAsync(sc => sc.CycleCompleted);

        if (!allCyclesCompleted)
            return;

        crew.CurrentSeasonStartDate = DateTime.UtcNow;
        await _unitOfWork.SaveChangesAsync();

        await InitializeNewSeasonAsync(crewId);
    }

    private async Task InitializeNewSeasonAsync(int crewId)
    {
        var crew = await _unitOfWork.Set<Crew>().FindAsync(crewId);
        if (crew == null)
            return;

        var crewMembers = await _unitOfWork.Set<CrewMembership>()
            .Where(cm => cm.CrewId == crewId && !cm.IsBanned)
            .Include(cm => cm.User)
            .ToListAsync();

        var scoredMembers = new List<(CrewMembership membership, decimal score)>();
        foreach (var membership in crewMembers)
        {
            var score = await _calculationService.CalculatePriorityScoreAsync(membership.UserId, crewId);
            scoredMembers.Add((membership, score));
        }

        var orderedMembers = scoredMembers
            .OrderByDescending(sm => sm.score)
            .ToList();

        int position = 0;
        foreach (var (membership, score) in orderedMembers)
        {
            var isMember = await _calculationService.IsMemberAsync(membership.UserId, crewId);
            var cycleCap = await _calculationService.CalculateCycleCapForMemberAsync(crewId, isMember);

            var cycle = new SeasonCycle
            {
                CrewId = crewId,
                UserId = membership.UserId,
                SeasonStartDate = crew.CurrentSeasonStartDate,
                CycleCapAtStart = cycleCap,
                TotalReceptionAmount = 0,
                SurvivalThresholdReceived = 0,
                CycleReceived = 0,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = score,
                ReceptionOrderPosition = position++
            };

            await _unitOfWork.Set<SeasonCycle>().AddAsync(cycle);
        }

        await _unitOfWork.SaveChangesAsync();
    }
}
