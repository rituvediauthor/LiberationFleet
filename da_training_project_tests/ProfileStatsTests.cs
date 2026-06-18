using da_training_project_tests.Support;
using LiberationFleet.Server.Application.Features.Profile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace da_training_project_tests;

public class ProfileStatsTests
{
    [Fact]
    public async Task ProfileMapper_PriorityScore_DisplayedInStats()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, user) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var member = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Member", emergencyLevel: 3, needsSurvivalAid: true);
        user.InNeedOfAid = true;
        user.EmergencyLevel = 3;
        user.NeedsSurvivalAid = true;
        await context.SaveChangesAsync();

        await MutualAidTestFixture.SeedContributionGiftAsync(
            context, crew.Id, member.Id, user.Id, 60m, DateTime.UtcNow.AddMonths(-1));

        var handler = MutualAidTestFixture.CreateGetMyProfileHandler(context, member.Id);
        var profile = await handler.Handle(
            new LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile.GetMyProfileQuery(),
            CancellationToken.None);

        profile.Should().NotBeNull();
        profile!.Stats.PriorityScore.Should().NotBe(0);
        profile.Stats.LifetimeContributions.Should().Be(60m);
        profile.Stats.MembershipStatus.Should().BeTrue();
    }

    [Fact]
    public void ProfileMapper_AverageMonthly_DividesLast3MonthsByThree()
    {
        var user = new User
        {
            Id = 1,
            Username = "AvgUser",
            Email = "avg@profile.com",
            PasswordHash = "hash",
            PaymentPlatforms = new List<UserPaymentPlatform>()
        };

        var giftStats = new UserGiftStats
        {
            LifetimeContributions = 300m,
            SacrificeCountLastYear = 5,
            ContributionsLast3Months = 90m,
            ReceptionLastYear = 100m
        };

        var profile = ProfileMapper.MapUser(user, giftStats, isMember: false, priorityScore: 42m);
        profile.Stats.AverageMonthlyContributions.Should().Be(30m);
    }

    [Fact]
    public void ProfileMapper_PercentBoost_DefaultsToZero()
    {
        var user = new User
        {
            Id = 1,
            Username = "BoostUser",
            Email = "boost@profile.com",
            PasswordHash = "hash",
            PaymentPlatforms = new List<UserPaymentPlatform>()
        };

        var giftStats = new UserGiftStats();
        var profile = ProfileMapper.MapUser(user, giftStats, isMember: false, priorityScore: 0m);

        profile.Stats.PercentBoost.Should().Be(0);
    }

    [Fact]
    public void ProfileMapper_MapsPaymentPlatforms()
    {
        var user = new User
        {
            Id = 1,
            Username = "PlatUser",
            Email = "plat@profile.com",
            PasswordHash = "hash",
            PaymentPlatforms = new List<UserPaymentPlatform>
            {
                new() { Id = 1, PaymentPlatformId = 1, Handle = "user@paypal", PaymentPlatform = new PaymentPlatform { Id = 1, Name = "PayPal" } },
                new() { Id = 2, PaymentPlatformId = 2, Handle = "user@cashapp", PaymentPlatform = new PaymentPlatform { Id = 2, Name = "Cash App" } }
            }
        };

        var profile = ProfileMapper.MapUser(user, new UserGiftStats(), isMember: true, priorityScore: 10m);

        profile.PaymentPlatforms.Should().HaveCount(2);
        profile.PaymentPlatforms[0].Platform.Should().Be("PayPal");
        profile.PaymentPlatforms[1].Platform.Should().Be("Cash App");
    }

    [Fact]
    public void ProfileMapper_MapsAidPreferences()
    {
        var user = new User
        {
            Id = 1,
            Username = "AidUser",
            Email = "aid@profile.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 4,
            NeedsSurvivalAid = true,
            PaymentPlatforms = new List<UserPaymentPlatform>()
        };

        var profile = ProfileMapper.MapUser(user, new UserGiftStats(), isMember: false, priorityScore: 5m);

        profile.InNeedOfAid.Should().BeTrue();
        profile.EmergencyLevel.Should().Be(4);
        profile.NeedsSurvivalAid.Should().BeTrue();
    }
}
