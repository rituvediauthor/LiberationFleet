using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Services;

public class IntermediaryFailureTrackingTests
{
    [Fact]
    public async Task RecordIntermediaryFailure_TwoConsecutive_StripsRole()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await SetIntermediaryAsync(fixture, fixture.Carol);

        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);
        membership = await ReloadAsync(fixture, fixture.Carol);
        membership.IsIntermediary.Should().BeTrue();
        membership.IntermediaryFailedCompletions.Should().Be(1);

        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);
        membership = await ReloadAsync(fixture, fixture.Carol);
        membership.IsIntermediary.Should().BeFalse();
        membership.IntermediaryFailedCompletions.Should().Be(0);
    }

    [Fact]
    public async Task RecordIntermediarySuccess_ResetsConsecutiveStreakOnly()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await SetIntermediaryAsync(fixture, fixture.Carol);

        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);
        await fixture.Service.RecordIntermediarySuccessAsync(fixture.Crew.Id, fixture.Carol.Id);

        var membership = await ReloadAsync(fixture, fixture.Carol);
        membership.IsIntermediary.Should().BeTrue();
        membership.IntermediaryFailedCompletions.Should().Be(0);
        membership.IntermediaryFailuresInMonth.Should().Be(1);

        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);
        membership = await ReloadAsync(fixture, fixture.Carol);
        // Second failure same month strips even though consecutive was reset.
        membership.IsIntermediary.Should().BeFalse();
    }

    [Fact]
    public async Task RecordIntermediaryFailure_TwoInSameMonthWithSuccessBetween_StillStrips()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await SetIntermediaryAsync(fixture, fixture.Carol);

        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);
        await fixture.Service.RecordIntermediarySuccessAsync(fixture.Crew.Id, fixture.Carol.Id);
        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);

        var membership = await ReloadAsync(fixture, fixture.Carol);
        membership.IsIntermediary.Should().BeFalse();
    }

    [Fact]
    public async Task RecordIntermediaryFailure_NonConsecutiveAcrossMonths_DoesNotStrip()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await SetIntermediaryAsync(fixture, fixture.Carol);

        membership.IntermediaryFailedCompletions = 0;
        membership.IntermediaryFailureMonthKey = 202501;
        membership.IntermediaryFailuresInMonth = 1;
        await fixture.Context.SaveChangesAsync();

        // Simulate a success that cleared the consecutive streak after January's failure.
        await fixture.Service.RecordIntermediarySuccessAsync(fixture.Crew.Id, fixture.Carol.Id);

        // One failure in the current month only.
        await fixture.Service.RecordIntermediaryFailureAsync(fixture.Crew.Id, fixture.Carol.Id);

        membership = await ReloadAsync(fixture, fixture.Carol);
        membership.IsIntermediary.Should().BeTrue();
        membership.IntermediaryFailedCompletions.Should().Be(1);
        membership.IntermediaryFailuresInMonth.Should().Be(1);
        var expectedMonthKey = DateTime.UtcNow.Year * 100 + DateTime.UtcNow.Month;
        membership.IntermediaryFailureMonthKey.Should().Be(expectedMonthKey);
    }

    private static async Task<CrewMembership> SetIntermediaryAsync(MutualAidSeasonFixture fixture, Domain.Entities.User user)
    {
        var membership = await fixture.Context.CrewMemberships
            .SingleAsync(m => m.UserId == user.Id && m.CrewId == fixture.Crew.Id);
        membership.IsIntermediary = true;
        membership.IntermediaryFailedCompletions = 0;
        membership.IntermediaryFailuresInMonth = 0;
        membership.IntermediaryFailureMonthKey = 0;
        await fixture.Context.SaveChangesAsync();
        return membership;
    }

    private static Task<CrewMembership> ReloadAsync(MutualAidSeasonFixture fixture, Domain.Entities.User user) =>
        fixture.Context.CrewMemberships.SingleAsync(m => m.UserId == user.Id && m.CrewId == fixture.Crew.Id);
}
