using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Services;

public class JoinActiveSeasonTests
{
    [Fact]
    public async Task MarkSeasonReadyAsync_AppendsJoinerWithoutRewritingCustomOrder()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(useSqlite: true);
        var seasonStart = fixture.SeasonStart;

        var alice = await PrimaryAsync(fixture, fixture.Alice.Id);
        var carol = await PrimaryAsync(fixture, fixture.Carol.Id);
        var bob = await PrimaryAsync(fixture, fixture.Bob.Id);
        alice.ReceptionOrderPosition = 0;
        alice.PriorityScoreAtSeasonStart = 100m;
        alice.HasCycleStarted = true;
        carol.ReceptionOrderPosition = 1;
        carol.PriorityScoreAtSeasonStart = 90m;
        bob.ReceptionOrderPosition = 3;
        bob.PriorityScoreAtSeasonStart = 10m;

        var emergency = new EmergencyRequest
        {
            CrewId = fixture.Crew.Id,
            RequesterUserId = fixture.Bob.Id,
            Purpose = "Custom-order package",
            AmountNeeded = 40m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.EmergencyRequests.Add(emergency);
        await fixture.Context.SaveChangesAsync();

        var segment = new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = fixture.Bob.Id,
            SeasonStartDate = seasonStart,
            EmergencyRequestId = emergency.Id,
            CycleCapAtStart = 40m,
            UsesSegmentCap = true,
            PriorityScoreAtSeasonStart = 10m,
            ReceptionOrderPosition = 2
        };
        fixture.Context.SeasonCycles.Add(segment);
        await fixture.Context.SaveChangesAsync();

        var dana = await AddJoinerAsync(fixture, "dana");
        dana.EmergencyLevel = 3;
        dana.PeopleRepresentedCount = 4;
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.MarkSeasonReadyAsync(dana.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Joined season.");

        var membership = await fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == dana.Id);
        membership.IsInSeason.Should().BeTrue();

        (await ReloadPositionAsync(fixture, alice.Id)).Should().Be(0);
        (await ReloadPositionAsync(fixture, carol.Id)).Should().Be(1);
        (await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == segment.Id)).ReceptionOrderPosition.Should().Be(2);
        (await ReloadPositionAsync(fixture, bob.Id)).Should().Be(3);

        var danaPrimary = await PrimaryAsync(fixture, dana.Id);
        danaPrimary.ReceptionOrderPosition.Should().Be(4);
        danaPrimary.CycleCompleted.Should().BeFalse();
        await AssertOnePrimaryPerUserAsync(fixture, seasonStart);

        var nextPrimaries = await fixture.Context.SeasonCycles
            .Where(c => c.UserId == dana.Id
                && c.SeasonStartDate == fixture.Crew.NextSeasonStartDate
                && c.EmergencyRequestId == null
                && c.EmergencySplitOfferId == null)
            .ToListAsync();
        nextPrimaries.Should().ContainSingle();
    }

    [Fact]
    public async Task MarkSeasonReadyAsync_WhenOrderWasCleared_StillJoins()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(useSqlite: true);
        var currentCycles = await fixture.Context.SeasonCycles
            .Where(c => c.SeasonStartDate == fixture.SeasonStart)
            .ToListAsync();
        fixture.Context.SeasonCycles.RemoveRange(currentCycles);
        await fixture.Context.SaveChangesAsync();

        var dana = await AddJoinerAsync(fixture, "dana");

        var result = await fixture.Service.MarkSeasonReadyAsync(dana.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        var primary = await PrimaryAsync(fixture, dana.Id);
        primary.ReceptionOrderPosition.Should().Be(0);
        primary.CycleCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task MarkSeasonReadyAsync_KeepsPrimaryAlreadyPlacedByReceptionEditor()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(useSqlite: true);
        var dana = await AddJoinerAsync(fixture, "dana");
        fixture.Context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = dana.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 600m,
            PriorityScoreAtSeasonStart = 1m,
            ReceptionOrderPosition = 7
        });
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.MarkSeasonReadyAsync(dana.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        var primaries = await fixture.Context.SeasonCycles
            .Where(c => c.UserId == dana.Id
                && c.SeasonStartDate == fixture.SeasonStart
                && c.EmergencyRequestId == null
                && c.EmergencySplitOfferId == null)
            .ToListAsync();
        primaries.Should().ContainSingle();
        primaries[0].ReceptionOrderPosition.Should().Be(7);
        primaries[0].CycleCompleted.Should().BeFalse();
    }

    [Fact]
    public async Task MarkSeasonReadyAsync_ReopensUnsatisfiedCycleClosedWhenMemberLeft()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync(useSqlite: true);
        var dana = await AddJoinerAsync(fixture, "dana");
        var closed = new SeasonCycle
        {
            CrewId = fixture.Crew.Id,
            UserId = dana.Id,
            SeasonStartDate = fixture.SeasonStart,
            CycleCapAtStart = 600m,
            CycleReceived = 0m,
            CycleCompleted = true,
            CycleCompletedAt = DateTime.UtcNow.AddDays(-1),
            PriorityScoreAtSeasonStart = 1m,
            ReceptionOrderPosition = 6
        };
        fixture.Context.SeasonCycles.Add(closed);
        await fixture.Context.SaveChangesAsync();

        var result = await fixture.Service.MarkSeasonReadyAsync(dana.Id, CancellationToken.None);

        result.Success.Should().BeTrue();
        var primary = await PrimaryAsync(fixture, dana.Id);
        primary.Id.Should().Be(closed.Id);
        primary.CycleCompleted.Should().BeFalse();
        primary.CycleCompletedAt.Should().BeNull();
        primary.ReceptionOrderPosition.Should().Be(6);
    }

    private static async Task<User> AddJoinerAsync(MutualAidSeasonFixture fixture, string username)
    {
        var user = new User
        {
            Username = username,
            DisplayName = username,
            Email = $"{username}@example.com",
            PasswordHash = "hash",
            CreatedAt = DateTime.UtcNow,
            IsActive = true,
            InNeedOfAid = true
        };
        fixture.Context.Users.Add(user);
        await fixture.Context.SaveChangesAsync();

        fixture.Context.CrewMemberships.Add(new CrewMembership
        {
            UserId = user.Id,
            CrewId = fixture.Crew.Id,
            JoinedAt = DateTime.UtcNow,
            EstimatedMonthlyContribution = 25m,
            IsSeasonReady = false,
            IsInSeason = false
        });
        fixture.Context.UserPaymentPlatforms.Add(new UserPaymentPlatform
        {
            UserId = user.Id,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            PlatformName = "PayPal",
            Handle = $"@{username}",
            IsPreferred = true
        });
        await fixture.Context.SaveChangesAsync();
        return user;
    }

    private static Task<SeasonCycle> PrimaryAsync(MutualAidSeasonFixture fixture, int userId) =>
        fixture.Context.SeasonCycles.SingleAsync(c =>
            c.UserId == userId
            && c.SeasonStartDate == fixture.SeasonStart
            && c.EmergencyRequestId == null
            && c.EmergencySplitOfferId == null);

    private static async Task<int> ReloadPositionAsync(MutualAidSeasonFixture fixture, int cycleId)
    {
        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c => c.Id == cycleId);
        return cycle.ReceptionOrderPosition;
    }

    private static async Task AssertOnePrimaryPerUserAsync(MutualAidSeasonFixture fixture, DateTime seasonStart)
    {
        var primaries = await fixture.Context.SeasonCycles
            .Where(c => c.CrewId == fixture.Crew.Id
                && c.SeasonStartDate == seasonStart
                && c.EmergencyRequestId == null
                && c.EmergencySplitOfferId == null)
            .ToListAsync();
        primaries.Select(c => c.UserId).Should().OnlyHaveUniqueItems();
    }
}
