using LiberationFleet.Server.Application.Features.Profile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Domain.Entities;

namespace da_training_project_tests;

public class ProfileStatsTests
{
    [Fact]
    public void ProfileMapper_PriorityScore_DisplayedInStats()
    {
        var user = new User
        {
            Id = 1,
            Username = "TestUser",
            Email = "test@profile.com",
            PasswordHash = "hash",
            InNeedOfAid = true,
            EmergencyLevel = 3,
            NeedsSurvivalAid = true,
            PaymentPlatforms = new List<UserPaymentPlatform>()
        };

        var giftStats = new UserGiftStats
        {
            LifetimeContributions = 500m,
            SacrificeCountLastYear = 10,
            ContributionsLast3Months = 150m,
            ReceptionLastYear = 200m
        };

        var profile = ProfileMapper.MapUser(user, giftStats, hasActiveCrewMembership: true);

        profile.Stats.Should().NotBeNull();
        profile.Stats.LifetimeContributions.Should().Be(500m);
        profile.Stats.AverageMonthlyContributions.Should().Be(50m);
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

        var profile = ProfileMapper.MapUser(user, giftStats, hasActiveCrewMembership: false);

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

        var giftStats = new UserGiftStats
        {
            LifetimeContributions = 0m,
            SacrificeCountLastYear = 0,
            ContributionsLast3Months = 0m,
            ReceptionLastYear = 0m
        };

        var profile = ProfileMapper.MapUser(user, giftStats, hasActiveCrewMembership: false);

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

        var giftStats = new UserGiftStats();

        var profile = ProfileMapper.MapUser(user, giftStats, hasActiveCrewMembership: true);

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

        var giftStats = new UserGiftStats();

        var profile = ProfileMapper.MapUser(user, giftStats, hasActiveCrewMembership: false);

        profile.InNeedOfAid.Should().BeTrue();
        profile.EmergencyLevel.Should().Be(4);
        profile.NeedsSurvivalAid.Should().BeTrue();
    }
}
