using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Tests.Application.Common;

public class ContentTenureCalculatorTests
{
    private static readonly DateTime BaseUtc = new(2026, 1, 1, 12, 0, 0, DateTimeKind.Utc);

    [Fact]
    public void GetTenureDays_WithNoTenure_ReturnsZero()
    {
        ContentTenureCalculator.GetTenureDays((UserCrewContentTenure?)null, BaseUtc).Should().Be(0);
    }

    [Fact]
    public void GetTenureDays_WithRunningClock_IncludesElapsedDays()
    {
        var tenure = new UserCrewContentTenure
        {
            AccruedTicks = 0,
            ClockStartedAtUtc = BaseUtc.AddDays(-5)
        };

        ContentTenureCalculator.GetTenureDays(tenure, BaseUtc).Should().Be(5);
    }

    [Fact]
    public void Pause_AccruesElapsedTimeAndStopsClock()
    {
        var accrued = 0L;
        DateTime? clock = BaseUtc.AddDays(-3);

        ContentTenureCalculator.Pause(ref accrued, ref clock, BaseUtc);

        clock.Should().BeNull();
        accrued.Should().Be(TimeSpan.FromDays(3).Ticks);
    }

    [Fact]
    public void Resume_WhenPaused_StartsClock()
    {
        var accrued = TimeSpan.FromDays(2).Ticks;
        DateTime? clock = null;

        ContentTenureCalculator.Resume(ref accrued, ref clock, BaseUtc);

        clock.Should().Be(BaseUtc);
        accrued.Should().Be(TimeSpan.FromDays(2).Ticks);
    }

    [Fact]
    public void Resume_WhenAlreadyRunning_IsIdempotent()
    {
        var accrued = 0L;
        var started = BaseUtc.AddHours(-1);
        DateTime? clock = started;

        ContentTenureCalculator.Resume(ref accrued, ref clock, BaseUtc);

        clock.Should().Be(started);
    }

    [Fact]
    public void GetTenureDays_WithAccruedAndRunningClock_SumsBoth()
    {
        var accrued = TimeSpan.FromDays(2).Ticks;
        DateTime? clock = BaseUtc;
        var utcNow = BaseUtc.AddDays(1);

        ContentTenureCalculator.GetTenureDays(accrued, clock, utcNow).Should().Be(3);
    }
}
