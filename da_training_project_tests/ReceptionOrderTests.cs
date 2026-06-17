using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class ReceptionOrderTests
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
    public async Task ReceptionOrder_SurvivalThresholdRecipientsFirst_ThenCycleRecipients()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var savannah = new User { Username = "Savannah", Email = "savannah@ro.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 3, NeedsSurvivalAid = true };
        var dave = new User { Username = "Dave", Email = "dave@ro.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2, NeedsSurvivalAid = true };
        context.Users.AddRange(savannah, dave);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Order Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "ORDR5678",
            CreatedByUserId = savannah.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        context.MonthlySurvivalThresholds.AddRange(
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 3m,
                ReceptionOrderPosition = 1,
                Satisfied = false
            },
            new MonthlySurvivalThreshold
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                Year = now.Year,
                Month = now.Month,
                ThresholdAmount = 22m,
                ReceivedAmount = 0m,
                ReceptionOrderPosition = 2,
                Satisfied = false
            });

        var seasonStart = DateTime.UtcNow.AddDays(-30);
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = savannah.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 310m,
                TotalReceptionAmount = 110m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 110m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 500m,
                ReceptionOrderPosition = 3
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = dave.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 310m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 4
            });
        await context.SaveChangesAsync();

        var thresholds = await context.MonthlySurvivalThresholds
            .Where(m => m.CrewId == crew.Id && !m.Satisfied)
            .OrderBy(m => m.ReceptionOrderPosition)
            .ToListAsync();

        var cycles = await context.SeasonCycles
            .Where(s => s.CrewId == crew.Id && !s.CycleCompleted)
            .OrderBy(s => s.ReceptionOrderPosition)
            .ToListAsync();

        thresholds.First().ReceptionOrderPosition.Should().BeLessThan(cycles.First().ReceptionOrderPosition);
    }

    [Fact]
    public async Task ReceptionOrder_LimitedTo30Entries()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var users = new List<User>();
        for (int i = 0; i < 35; i++)
        {
            users.Add(new User
            {
                Username = $"User{i}",
                Email = $"user{i}@ro.com",
                PasswordHash = "hash",
                InNeedOfAid = true,
                EmergencyLevel = 1
            });
        }
        context.Users.AddRange(users);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Large Crew",
            MaxSize = 50,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "LRGE1234",
            CreatedByUserId = users[0].Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var seasonStart = DateTime.UtcNow.AddDays(-30);
        for (int i = 0; i < 35; i++)
        {
            context.SeasonCycles.Add(new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = users[i].Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 100m - i,
                ReceptionOrderPosition = i + 1
            });
        }
        await context.SaveChangesAsync();

        var displayedEntries = await context.SeasonCycles
            .Where(s => s.CrewId == crew.Id)
            .OrderBy(s => s.ReceptionOrderPosition)
            .Take(30)
            .ToListAsync();

        displayedEntries.Should().HaveCount(30);
    }

    [Fact]
    public async Task ReceptionOrder_EmergencyLevelChange_MovesCrewmateInOrder()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var alice = new User { Username = "Alice", Email = "alice@emg.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 1 };
        var bob = new User { Username = "Bob", Email = "bob@emg.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2 };
        var charlie = new User { Username = "Charlie", Email = "charlie@emg.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 3 };
        context.Users.AddRange(alice, bob, charlie);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Emergency Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "EMRG1234",
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
                UserId = charlie.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 50m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 50m,
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
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = alice.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 100m,
                ReceptionOrderPosition = 3
            });
        await context.SaveChangesAsync();

        alice.EmergencyLevel = 10;
        await context.SaveChangesAsync();

        var aliceCycle = await context.SeasonCycles.FirstAsync(s => s.UserId == alice.Id && s.CrewId == crew.Id);
        var charlieCycle = await context.SeasonCycles.FirstAsync(s => s.UserId == charlie.Id && s.CrewId == crew.Id);

        charlieCycle.ReceptionOrderPosition.Should().Be(1);
    }

    [Fact]
    public async Task ReceptionOrder_MovedCrewmate_CannotJumpAheadOfCurrentCycleRecipient()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var current = new User { Username = "Current", Email = "current@emg.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 2 };
        var mover = new User { Username = "Mover", Email = "mover@emg.com", PasswordHash = "hash", InNeedOfAid = true, EmergencyLevel = 1 };
        context.Users.AddRange(current, mover);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Jump Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "JUMP1234",
            CreatedByUserId = current.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var seasonStart = DateTime.UtcNow.AddDays(-30);
        context.SeasonCycles.AddRange(
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = current.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 100m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 100m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 300m,
                ReceptionOrderPosition = 1
            },
            new SeasonCycle
            {
                CrewId = crew.Id,
                UserId = mover.Id,
                SeasonStartDate = seasonStart,
                CycleCapAtStart = 200m,
                TotalReceptionAmount = 0m,
                SurvivalThresholdReceived = 0m,
                CycleReceived = 0m,
                CycleCompleted = false,
                PriorityScoreAtSeasonStart = 100m,
                ReceptionOrderPosition = 2
            });
        await context.SaveChangesAsync();

        var currentCycle = await context.SeasonCycles.FirstAsync(s => s.UserId == current.Id);
        var moverCycle = await context.SeasonCycles.FirstAsync(s => s.UserId == mover.Id);

        moverCycle.ReceptionOrderPosition.Should().BeGreaterThan(currentCycle.ReceptionOrderPosition);
    }
}
