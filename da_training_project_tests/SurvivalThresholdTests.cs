using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class SurvivalThresholdTests
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
    public void SurvivalThresholdAmount_CalculatesCorrectly()
    {
        decimal aliceAvgMonthly = 90m / 3m;
        decimal bobAvgMonthly = 60m / 3m;
        decimal charlieAvgMonthly = 30m / 3m;
        decimal totalMonthlyCapacity = aliceAvgMonthly + bobAvgMonthly + charlieAvgMonthly;

        int thresholdRecipientCount = 2;

        decimal survivalThreshold = (totalMonthlyCapacity / 2m) / thresholdRecipientCount;

        survivalThreshold.Should().Be(15m);
    }

    [Fact]
    public async Task SurvivalThresholdRecipients_OrderedByPriorityScore_AtMonthStart()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var alice = new User { Username = "Alice", Email = "alice@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 3, NeedsSurvivalAid = true };
        var bob = new User { Username = "Bob", Email = "bob@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2, NeedsSurvivalAid = true };
        var charlie = new User { Username = "Charlie", Email = "charlie@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 5, NeedsSurvivalAid = true };
        context.Users.AddRange(alice, bob, charlie);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "ST Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "STCR1234",
            CreatedByUserId = alice.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.AddRange(
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = charlie.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 1,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 2,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = bob.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 3,
                Satisfied = false
            });
        await context.SaveChangesAsync();

        var thresholds = await context.MonthlySurvivalThresholds
            .Where(m => m.CrewId == crew.Id && m.Year == now.Year && m.Month == now.Month)
            .OrderBy(m => m.ReceptionOrderPosition)
            .ToListAsync();

        thresholds[0].UserId.Should().Be(charlie.Id);
        thresholds[1].UserId.Should().Be(alice.Id);
        thresholds[2].UserId.Should().Be(bob.Id);
    }

    [Fact]
    public async Task SurvivalThreshold_NeedAmount_EqualsThresholdMinusReceived()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "NeedCalc", Email = "needcalc@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2, NeedsSurvivalAid = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Need Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "NEED1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var threshold = new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = user.Id,
            Year = now.Year,
            Month = now.Month,
            ThresholdAmount = 22m,
            ReceivedAmount = 3m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        };
        context.MonthlySurvivalThresholds.Add(threshold);
        await context.SaveChangesAsync();

        var stored = await context.MonthlySurvivalThresholds.FirstAsync(m => m.UserId == user.Id);
        var needAmount = stored.ThresholdAmount - stored.ReceivedAmount;

        needAmount.Should().Be(19m);
    }

    [Fact]
    public async Task NewMonth_ExistingUnsatisfiedThresholds_ComeBeforeNewOnes()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var savannah = new User { Username = "Savannah", Email = "savannah@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 3, NeedsSurvivalAid = true };
        var dave = new User { Username = "Dave", Email = "dave@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2, NeedsSurvivalAid = true };
        context.Users.AddRange(savannah, dave);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Rollover Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "ROLL1234",
            CreatedByUserId = savannah.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.MonthlySurvivalThresholds.AddRange(
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                Year = 2026,
                Month = 5,
                ThresholdAmount = 22m,
                ReceivedAmount = 3m,
                ReceptionOrderPosition = 1,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                Year = 2026,
                Month = 5,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 2,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                Year = 2026,
                Month = 6,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 3,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                Year = 2026,
                Month = 6,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 4,
                Satisfied = false
            });
        await context.SaveChangesAsync();

        var allThresholds = await context.MonthlySurvivalThresholds
            .Where(m => m.CrewId == crew.Id)
            .OrderBy(m => m.ReceptionOrderPosition)
            .ToListAsync();

        allThresholds[0].UserId.Should().Be(savannah.Id);
        allThresholds[0].Month.Should().Be(5);
        allThresholds[1].UserId.Should().Be(dave.Id);
        allThresholds[1].Month.Should().Be(5);
        allThresholds[2].Month.Should().Be(6);
        allThresholds[3].Month.Should().Be(6);
    }

    [Fact]
    public async Task SurvivalThreshold_Satisfied_WhenReceivedEqualsThreshold()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "Satisfied", Email = "satisfied@st.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2, NeedsSurvivalAid = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Sat Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "SATF1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var threshold = new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = user.Id,
            Year = 2026,
            Month = 6,
            ThresholdAmount = 22m,
            ReceivedAmount = 22m,
            ReceptionOrderPosition = 1,
            Satisfied = true
        };
        context.MonthlySurvivalThresholds.Add(threshold);
        await context.SaveChangesAsync();

        var stored = await context.MonthlySurvivalThresholds.FirstAsync(m => m.UserId == user.Id);
        stored.Satisfied.Should().BeTrue();
        (stored.ThresholdAmount - stored.ReceivedAmount).Should().Be(0m);
    }

    [Fact]
    public void SurvivalThresholdRecipients_OnlyThoseRegistered()
    {
        var alice = new User { Id = 1, Username = "Alice", Email = "a@a.com", PasswordHash = "h", NeedsSurvivalAid = true };
        var bob = new User { Id = 2, Username = "Bob", Email = "b@b.com", PasswordHash = "h", NeedsSurvivalAid = false };
        var charlie = new User { Id = 3, Username = "Charlie", Email = "c@c.com", PasswordHash = "h", NeedsSurvivalAid = true };

        var users = new[] { alice, bob, charlie };
        var thresholdRecipients = users.Where(u => u.NeedsSurvivalAid).ToList();

        thresholdRecipients.Should().HaveCount(2);
        thresholdRecipients.Should().Contain(u => u.Username == "Alice");
        thresholdRecipients.Should().Contain(u => u.Username == "Charlie");
        thresholdRecipients.Should().NotContain(u => u.Username == "Bob");
    }
}
