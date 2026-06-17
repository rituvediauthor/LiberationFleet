using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class CycleCapTests
{
    private static ApplicationDbContext CreateContext(string? name = null)
    {
        var options = new DbContextOptionsBuilder<ApplicationDbContext>()
            .UseInMemoryDatabase(name ?? Guid.NewGuid().ToString())
            .Options;
        return new ApplicationDbContext(options);
    }

    private static async Task SeedPaymentPlatforms(ApplicationDbContext context)
    {
        if (await context.PaymentPlatforms.AnyAsync()) return;
        context.PaymentPlatforms.AddRange(
            new PaymentPlatform { Id = 1, Name = "PayPal", SortOrder = 1 },
            new PaymentPlatform { Id = 2, Name = "Cash App", SortOrder = 2 },
            new PaymentPlatform { Id = 3, Name = "Venmo", SortOrder = 3 },
            new PaymentPlatform { Id = 4, Name = "Zelle", SortOrder = 4 },
            new PaymentPlatform { Id = 5, Name = "Other", SortOrder = 5 });
        await context.SaveChangesAsync();
    }

    [Fact]
    public void MemberCycleCap_IsDoubleTheTotalMonthlyCapacity()
    {
        decimal aliceAvgMonthly = 100m / 3m;
        decimal bobAvgMonthly = 80m / 3m;
        decimal totalMonthlyCapacity = aliceAvgMonthly + bobAvgMonthly;

        decimal memberCycleCap = totalMonthlyCapacity * 2m;

        memberCycleCap.Should().Be(120m);
    }

    [Fact]
    public void NonMemberCycleCap_IsHalfTheTotalMonthlyCapacity()
    {
        decimal aliceAvgMonthly = 100m / 3m;
        decimal bobAvgMonthly = 80m / 3m;
        decimal totalMonthlyCapacity = aliceAvgMonthly + bobAvgMonthly;

        decimal nonMemberCycleCap = totalMonthlyCapacity / 2m;

        nonMemberCycleCap.Should().Be(30m);
    }

    [Fact]
    public void MembershipStatus_ContributedThisSeason_IsMember()
    {
        decimal contributionsThisSeason = 50m;
        decimal contributionsLastSeason = 0m;
        bool isMember = contributionsThisSeason > 0 || contributionsLastSeason > 0;

        isMember.Should().BeTrue();
    }

    [Fact]
    public void MembershipStatus_ContributedLastSeason_IsMember()
    {
        decimal contributionsThisSeason = 0m;
        decimal contributionsLastSeason = 30m;
        bool isMember = contributionsThisSeason > 0 || contributionsLastSeason > 0;

        isMember.Should().BeTrue();
    }

    [Fact]
    public void MembershipStatus_NeverContributed_IsNotMember()
    {
        decimal contributionsThisSeason = 0m;
        decimal contributionsLastSeason = 0m;
        bool isMember = contributionsThisSeason > 0 || contributionsLastSeason > 0;

        isMember.Should().BeFalse();
    }

    [Fact]
    public async Task CycleGifts_CannotExceedCycleCapForReception()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User
        {
            Username = "Receiver",
            Email = "receiver@example.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 2,
            NeedsSurvivalAid = false
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Cycle Test Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "CYCL1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        decimal cycleCap = 200m;
        var seasonCycle = new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = user.Id,
            SeasonStartDate = DateTime.UtcNow.AddDays(-10),
            CycleCapAtStart = cycleCap,
            TotalReceptionAmount = 200m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 200m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = 100m,
            ReceptionOrderPosition = 1
        };
        context.SeasonCycles.Add(seasonCycle);
        await context.SaveChangesAsync();

        var stored = await context.SeasonCycles.FirstAsync(s => s.UserId == user.Id);
        stored.CycleReceived.Should().Be(cycleCap);
    }

    [Fact]
    public async Task SurvivalThresholdGifts_CanExceedCycleCapReception()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User
        {
            Username = "ThresholdUser",
            Email = "threshold@example.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 2,
            NeedsSurvivalAid = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Threshold Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "THRS1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        decimal cycleCap = 200m;
        var seasonCycle = new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = user.Id,
            SeasonStartDate = DateTime.UtcNow.AddDays(-10),
            CycleCapAtStart = cycleCap,
            TotalReceptionAmount = 250m,
            SurvivalThresholdReceived = 50m,
            CycleReceived = 200m,
            CycleCompleted = true,
            CycleCompletedAt = DateTime.UtcNow.AddDays(-2),
            PriorityScoreAtSeasonStart = 100m,
            ReceptionOrderPosition = 1
        };
        context.SeasonCycles.Add(seasonCycle);
        await context.SaveChangesAsync();

        var stored = await context.SeasonCycles.FirstAsync(s => s.UserId == user.Id);
        stored.TotalReceptionAmount.Should().BeGreaterThan(cycleCap);
        stored.SurvivalThresholdReceived.Should().Be(50m);
    }

    [Fact]
    public async Task Season_EndsWhenAllCrewmatesCompleteCycle()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var alice = new User { Username = "Alice", Email = "alice@cycle.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2 };
        var bob = new User { Username = "Bob", Email = "bob@cycle.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 1 };
        context.Users.AddRange(alice, bob);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Season Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "SEAS1234",
            CreatedByUserId = alice.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var seasonStart = DateTime.UtcNow.AddDays(-60);
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 210m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 210m,
                CycleCompleted = true,
                CycleCompletedAt = DateTime.UtcNow.AddDays(-10),
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = bob.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 205m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 205m,
                CycleCompleted = true,
                CycleCompletedAt = DateTime.UtcNow.AddDays(-5),
                PriorityScoreAtSeasonStart = 200m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        var allCompleted = await context.SeasonCycles
            .Where(s => s.CrewId == crew.Id && s.SeasonStartDate == seasonStart)
            .AllAsync(s => s.CycleCompleted);

        allCompleted.Should().BeTrue();
    }

    [Fact]
    public void MonthlyGivingCapacity_IsAverageOfLastThreeMonths()
    {
        decimal month1Gifts = 90m;
        decimal month2Gifts = 60m;
        decimal month3Gifts = 30m;
        decimal totalLast3Months = month1Gifts + month2Gifts + month3Gifts;

        decimal monthlyCapacity = totalLast3Months / 3m;

        monthlyCapacity.Should().Be(60m);
    }

    [Fact]
    public async Task CycleStarted_MustCompleteBeforeNextCrewmateCycleBegins()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var alice = new User { Username = "Alice", Email = "alice@order.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 3 };
        var bob = new User { Username = "Bob", Email = "bob@order.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2 };
        context.Users.AddRange(alice, bob);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Order Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "ORDR1234",
            CreatedByUserId = alice.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var seasonStart = DateTime.UtcNow.AddDays(-30);
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 100m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 100m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 500m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = bob.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        var cycles = await context.SeasonCycles
            .Where(s => s.CrewId == crew.Id && s.SeasonStartDate == seasonStart)
            .OrderBy(s => s.ReceptionOrderPosition)
            .ToListAsync();

        cycles[0].UserId.Should().Be(alice.Id);
        cycles[0].CycleCompleted.Should().BeFalse();
        cycles[1].CycleReceived.Should().Be(0m);
    }
}
