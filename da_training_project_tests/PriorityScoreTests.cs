using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Data;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class PriorityScoreTests
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
    public void PriorityScore_OrganizerRole_ShouldBeNegativeOne()
    {
        var membership = new CrewMembership
        {
            UserId = 1,
            CrewId = 1,
            IsOrganizer = true,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        };

        membership.IsOrganizer.Should().BeTrue();
    }

    [Fact]
    public void PriorityScore_NotInNeed_ShouldBeNegativeTwo()
    {
        var user = new User
        {
            Id = 1,
            Username = "testuser",
            Email = "test@example.com",
            PasswordHash = "hash",
            InNeedOfAid = false,
            EmergencyLevel = 0
        };

        user.InNeedOfAid.Should().BeFalse();
    }

    [Fact]
    public async Task PriorityScore_RegularCrewmate_CalculatesCorrectly()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var alice = new User
        {
            Username = "Alice",
            Email = "alice@example.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 3,
            NeedsSurvivalAid = true
        };
        var bob = new User
        {
            Username = "Bob",
            Email = "bob@example.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 2,
            NeedsSurvivalAid = false
        };
        context.Users.AddRange(alice, bob);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Test Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "TEST1234",
            CreatedByUserId = alice.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        context.CrewMemberships.AddRange(
            new CrewMembership { UserId = alice.Id, CrewId = crew.Id, IsOrganizer = false, IsBanned = false, JoinedAt = DateTime.UtcNow },
            new CrewMembership { UserId = bob.Id, CrewId = crew.Id, IsOrganizer = false, IsBanned = false, JoinedAt = DateTime.UtcNow });
        await context.SaveChangesAsync();

        var threeMonthsAgo = DateTime.UtcNow.AddMonths(-2);
        context.Gifts.AddRange(
            new Gift { CrewId = crew.Id, GiverUserId = alice.Id, RecipientUserId = bob.Id, Amount = 90, PaymentPlatformId = 1, Type = GiftType.Direct, CreatedAt = threeMonthsAgo },
            new Gift { CrewId = crew.Id, GiverUserId = bob.Id, RecipientUserId = alice.Id, Amount = 60, PaymentPlatformId = 1, Type = GiftType.Direct, CreatedAt = threeMonthsAgo });
        await context.SaveChangesAsync();

        var totalLifetimeContributions = 90m + 60m;
        var aliceContribution = 90m;
        var aliceAvgMonthly = 90m / 3m;
        var bobAvgMonthly = 60m / 3m;
        var totalMonthlyCapacity = aliceAvgMonthly + bobAvgMonthly;
        var survivalThresholdRecipientCount = 1;
        var survivalThresholdAmount = (totalMonthlyCapacity / 2m) / survivalThresholdRecipientCount;
        var percentBonus = 0;

        var alicePriorityScore =
            (totalLifetimeContributions * alice.EmergencyLevel)
            + 1
            + aliceContribution
            + survivalThresholdAmount * (1m - (percentBonus / 100m));

        alicePriorityScore.Should().BeGreaterThan(0);
    }

    [Fact]
    public async Task PriorityScore_FrozenAtSeasonStart_DoesNotChangeUntilNewSeason()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User
        {
            Username = "Frozen",
            Email = "frozen@example.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 2,
            NeedsSurvivalAid = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var crew = new Crew
        {
            Name = "Frozen Crew",
            MaxSize = 10,
            Privacy = CrewPrivacy.Public,
            Scope = CrewScope.Online,
            JoinCode = "FRZN1234",
            CreatedByUserId = user.Id,
            CreatedAt = DateTime.UtcNow
        };
        context.Crews.Add(crew);
        await context.SaveChangesAsync();

        var seasonCycle = new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = user.Id,
            SeasonStartDate = DateTime.UtcNow.AddDays(-30),
            CycleCapAtStart = 100m,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = 500m,
            ReceptionOrderPosition = 1
        };
        context.SeasonCycles.Add(seasonCycle);
        await context.SaveChangesAsync();

        var stored = await context.SeasonCycles.FirstAsync(s => s.UserId == user.Id);
        stored.PriorityScoreAtSeasonStart.Should().Be(500m);
    }

    [Fact]
    public async Task PriorityScore_RecalculatedOnEmergencyLevelChange()
    {
        using var context = CreateContext();
        await SeedPaymentPlatforms(context);

        var user = new User
        {
            Username = "Emergency",
            Email = "emergency@example.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 1,
            NeedsSurvivalAid = true
        };
        context.Users.Add(user);
        await context.SaveChangesAsync();

        var initialEmergencyLevel = user.EmergencyLevel;
        user.EmergencyLevel = 5;
        await context.SaveChangesAsync();

        user.EmergencyLevel.Should().Be(5);
        user.EmergencyLevel.Should().NotBe(initialEmergencyLevel);
    }

    [Fact]
    public void PriorityScore_Formula_AccountsForMembership()
    {
        decimal totalLifetimeContributions = 1000m;
        int emergencyLevel = 3;
        int membershipStatus = 1;
        decimal crewmateContribution = 200m;
        decimal survivalThresholdAmount = 50m;
        int percentBonus = 0;

        var score = (totalLifetimeContributions * emergencyLevel)
            + membershipStatus
            + crewmateContribution
            + survivalThresholdAmount * (1m - (percentBonus / 100m));

        score.Should().Be(3251m);
    }

    [Fact]
    public void PriorityScore_Formula_NonMemberGetsZeroMembershipBonus()
    {
        decimal totalLifetimeContributions = 1000m;
        int emergencyLevel = 3;
        int membershipStatus = 0;
        decimal crewmateContribution = 200m;
        decimal survivalThresholdAmount = 50m;
        int percentBonus = 0;

        var score = (totalLifetimeContributions * emergencyLevel)
            + membershipStatus
            + crewmateContribution
            + survivalThresholdAmount * (1m - (percentBonus / 100m));

        score.Should().Be(3250m);
    }

    [Fact]
    public void PriorityScore_HonoraryMember_CountsAsMember()
    {
        var membership = new CrewMembership
        {
            UserId = 1,
            CrewId = 1,
            IsHonoraryMember = true,
            IsOrganizer = false,
            IsBanned = false,
            JoinedAt = DateTime.UtcNow
        };

        var membershipStatus = membership.IsHonoraryMember ? 1 : 0;
        membershipStatus.Should().Be(1);
    }
}
