using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class GiftEntityTests
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
    public async Task Gift_IsSurvivalThreshold_CanBeSetToTrue()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "Giver", Email = "giver@ge.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "Recipient", Email = "recipient@ge.com", PasswordHash = "hash", IsActive = true, NeedsSurvivalAid = true };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Entity Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "ENTY1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var gift = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            Type = GiftType.Direct,
            Amount = 22m,
            PaymentPlatformId = 1,
            IsSurvivalThreshold = true,
            CreatedAt = DateTime.UtcNow
        };
        context.Gifts.Add(gift);
        await context.SaveChangesAsync();

        var stored = await context.Gifts.FirstAsync();
        stored.IsSurvivalThreshold.Should().BeTrue();
    }

    [Fact]
    public async Task Gift_CountsTowardReception_DefaultsToTrue()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "DefGiver", Email = "defgiver@ge.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "DefRecip", Email = "defrecip@ge.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Default Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "DEFT1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var gift = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            Type = GiftType.Direct,
            Amount = 20m,
            PaymentPlatformId = 1,
            CreatedAt = DateTime.UtcNow
        };
        context.Gifts.Add(gift);
        await context.SaveChangesAsync();

        var stored = await context.Gifts.FirstAsync();
        stored.CountsTowardReception.Should().BeTrue();
    }

    [Fact]
    public async Task Gift_InitiatedType_DoesNotCountTowardReceptionUntilCompleted()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var giver = new User { Username = "InitGiver", Email = "initgiver@ge.com", PasswordHash = "hash", IsActive = true };
        var recipient = new User { Username = "InitRecip", Email = "initrecip@ge.com", PasswordHash = "hash", IsActive = true };
        var middleman = new User { Username = "InitMiddle", Email = "initmiddle@ge.com", PasswordHash = "hash", IsActive = true };
        context.Users.AddRange(giver, recipient, middleman);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Init Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "INIT1234",
            CreatedByUserId = giver.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var initiated = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middleman.Id,
            Type = GiftType.Initiated,
            Amount = 50m,
            PaymentPlatformId = 1,
            CountsTowardReception = false,
            CreatedAt = DateTime.UtcNow
        };
        context.Gifts.Add(initiated);
        await context.SaveChangesAsync();

        var stored = await context.Gifts.FirstAsync();
        stored.CountsTowardReception.Should().BeFalse();
        stored.Type.Should().Be(GiftType.Initiated);
    }

    [Fact]
    public async Task SeasonCycle_UniqueConstraint_PerCrewUserSeason()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "Unique", Email = "unique@ge.com", PasswordHash = "hash", IsActive = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Unique Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "UNIQ1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var seasonStart = DateTime.UtcNow.AddDays(-30);
        context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = user.Id,
            SeasonStartDate = seasonStart,
            CycleCapAtStart = 200m,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = 100m,
            ReceptionOrderPosition = 1
        });
        await context.SaveChangesAsync();

        var count = await context.SeasonCycles
            .Where(s => s.CrewId == crew.Id && s.UserId == user.Id && s.SeasonStartDate == seasonStart)
            .CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task MonthlySurvivalThreshold_UniqueConstraint_PerCrewUserYearMonth()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "MonthUniq", Email = "monthuniq@ge.com", PasswordHash = "hash", IsActive = true, NeedsSurvivalAid = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Month Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "MNTH1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.MonthlySurvivalThresholds.Add(new MonthlySurvivalThreshold
        {
            CrewId = crew.Id,
            UserId = user.Id,
            Year = 2026,
            Month = 6,
            ThresholdAmount = 22m,
            ReceivedAmount = 0m,
            ReceptionOrderPosition = 1,
            Satisfied = false
        });
        await context.SaveChangesAsync();

        var count = await context.MonthlySurvivalThresholds
            .Where(m => m.CrewId == crew.Id && m.UserId == user.Id && m.Year == 2026 && m.Month == 6)
            .CountAsync();

        count.Should().Be(1);
    }

    [Fact]
    public async Task Crew_CurrentSeasonStartDate_Exists()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User { Username = "SeasonUser", Email = "seasonuser@ge.com", PasswordHash = "hash", IsActive = true };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var now = DateTime.UtcNow;
        var crew = new Crew
        {
            Name = "Season Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "SSDT1234",
            CreatedByUserId = user.Id,
            CreatedAt = now,
            CurrentSeasonStartDate = now
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var stored = await context.Crews.FirstAsync(c => c.Id == crew.Id);
        stored.CurrentSeasonStartDate.Should().BeCloseTo(now, TimeSpan.FromSeconds(1));
    }

    [Fact]
    public async Task User_PercentBonus_DefaultsToZero()
    {
        using var context = CreateContext();

        var user = new User
        {
            Username = "BonusUser",
            Email = "bonus@ge.com",
            PasswordHash = "hash",
            IsActive = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var stored = await context.Users.FirstAsync(u => u.Id == user.Id);
        stored.PercentBonus.Should().Be(0);
    }
}
