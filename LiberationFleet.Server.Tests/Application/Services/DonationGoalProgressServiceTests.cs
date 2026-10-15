using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Services;

public class DonationGoalProgressServiceTests
{
    [Fact]
    public async Task ApplyDonation_RollsExcessAcrossOneTimeGoals_AndSeedsYearFromSixMonths()
    {
        var goals = SeedGoals();
        var sut = CreateSut(goals);

        // $750 + $1000 + $50 spillover into live-one-year (which also inherits $1000)
        await sut.ApplyDonationAsync(180_000, utcNow: Utc(2026, 4, 15));

        Assert.True(goals[0].IsCompleted);
        Assert.Equal(75_000, goals[0].ProgressCents);
        Assert.True(goals[1].IsCompleted);
        Assert.Equal(100_000, goals[1].ProgressCents);
        Assert.False(goals[2].IsCompleted);
        Assert.Equal(105_000, goals[2].ProgressCents); // $1000 carry + $50 spillover
        Assert.Equal(new DateOnly(2025, 11, 1), goals[2].AnnualYearStart);
    }

    [Fact]
    public async Task ApplyDonation_CompletingSixMonthsExactly_LeavesYearAtLeastHalfFunded()
    {
        var goals = SeedGoals();
        goals[0].IsCompleted = true;
        goals[0].ProgressCents = goals[0].TargetCents;
        goals[1].ProgressCents = 99_000; // $10 shy

        var sut = CreateSut(goals);
        await sut.ApplyDonationAsync(1_000, utcNow: Utc(2026, 6, 1));

        Assert.True(goals[1].IsCompleted);
        Assert.Equal("Live for one year!", (await sut.GetCurrentAsync(Utc(2026, 6, 1))).Title);
        Assert.Equal(100_000, goals[2].ProgressCents); // inherited six-month total
        Assert.Equal(50, (await sut.GetCurrentAsync(Utc(2026, 6, 1))).Percent);
    }

    [Fact]
    public async Task ApplyDonation_TeammateRestartIncrementsCounterAndKeepsExcess()
    {
        var goals = SeedGoals();
        CompleteOneTimesAndAnnual(goals);
        goals[3].ProgressCents = 12_999_000; // $10 shy of $130k
        goals[3].CompletionsThisYear = 2;

        var sut = CreateSut(goals);
        await sut.ApplyDonationAsync(25_000, utcNow: Utc(2026, 5, 1)); // $250

        Assert.Equal(3, goals[3].CompletionsThisYear);
        Assert.Equal(24_000, goals[3].ProgressCents); // $240 leftover into new cycle
        Assert.False(goals[3].IsCompleted);
    }

    [Fact]
    public async Task NovemberPreempt_OnlyAfterOneTimesComplete_ResetsAnnualAndCounter()
    {
        var goals = SeedGoals();
        CompleteOneTimesAndAnnual(goals);
        goals[2].AnnualYearStart = new DateOnly(2025, 11, 1);
        goals[3].ProgressCents = 4_500_000;
        goals[3].CompletionsThisYear = 2;

        var sut = CreateSut(goals);
        var dto = await sut.GetCurrentAsync(Utc(2026, 11, 1, 12));

        Assert.Equal("Live for one year!", dto.Title);
        Assert.Equal(0, goals[2].ProgressCents);
        Assert.False(goals[2].IsCompleted);
        Assert.Equal(new DateOnly(2026, 11, 1), goals[2].AnnualYearStart);
        Assert.Equal(0, goals[3].CompletionsThisYear);
        Assert.Equal(4_500_000, goals[3].ProgressCents);
        Assert.Equal("Fund a teammate!", dto.NextGoalTitle);
    }

    [Fact]
    public async Task November_DoesNotPreemptWhileOneTimeGoalsRemain()
    {
        var goals = SeedGoals();
        goals[0].ProgressCents = 10_000;
        goals[2].ProgressCents = 50_000; // should not be wiped
        goals[2].AnnualYearStart = null;

        var sut = CreateSut(goals);
        var dto = await sut.GetCurrentAsync(Utc(2026, 11, 1, 12));

        Assert.Equal("Launch on the app store!", dto.Title);
        Assert.Equal(10_000, goals[0].ProgressCents);
        Assert.Equal(50_000, goals[2].ProgressCents);
        Assert.Null(goals[2].AnnualYearStart);
        Assert.False(DonationGoalProgressService.EnsureCalendarTransitions(goals, Utc(2026, 11, 1, 12)));
    }

    [Fact]
    public async Task DonationAfterNovember_AppliesToLiveOneYear()
    {
        var goals = SeedGoals();
        CompleteOneTimesAndAnnual(goals);
        goals[2].AnnualYearStart = new DateOnly(2025, 11, 1);
        goals[3].ProgressCents = 1_000_000;

        var sut = CreateSut(goals);
        await sut.ApplyDonationAsync(10_000, utcNow: Utc(2026, 11, 2));

        Assert.Equal(10_000, goals[2].ProgressCents);
        Assert.False(goals[2].IsCompleted);
        Assert.Equal(1_000_000, goals[3].ProgressCents);
    }

    [Fact]
    public async Task NovemberRestart_DoesNotReCreditSixMonthCarryover()
    {
        var goals = SeedGoals();
        // First year already finished after inheriting six months; cycle is unlocked.
        CompleteOneTimesAndAnnual(goals);
        goals[2].AnnualYearStart = new DateOnly(2025, 11, 1);
        goals[2].CompletedAt = Utc(2026, 3, 1);

        var sut = CreateSut(goals);
        await sut.GetCurrentAsync(Utc(2026, 11, 1, 12));

        Assert.Equal(0, goals[2].ProgressCents);
        Assert.False(goals[2].IsCompleted);

        // A later donation must not jump to $1000 from a phantom six-month re-seed.
        await sut.ApplyDonationAsync(5_000, utcNow: Utc(2026, 11, 2));
        Assert.Equal(5_000, goals[2].ProgressCents);
    }

    [Fact]
    public async Task GetCurrent_FormatsProgressLabelAndTeammateDescription()
    {
        var goals = SeedGoals();
        CompleteOneTimesAndAnnual(goals);
        goals[2].AnnualYearStart = new DateOnly(2025, 11, 1);
        goals[3].ProgressCents = 25_000; // $250
        goals[3].CompletionsThisYear = 1;

        var sut = CreateSut(goals);
        var dto = await sut.GetCurrentAsync(Utc(2026, 6, 1));

        Assert.Equal("Fund a teammate!", dto.Title);
        Assert.Equal("$250/$130000", dto.ProgressLabel);
        Assert.Contains("1 teammates funded this year!", dto.Description);
        Assert.Equal("Live for one year!", dto.NextGoalTitle);
    }

    [Fact]
    public void GetNovemberCycleStart_UsesEasternCalendar()
    {
        // 2026-11-01 03:30 UTC is still Oct 31 evening in Eastern (EDT UTC-4).
        var before = DonationGoalProgressService.GetNovemberCycleStart(Utc(2026, 11, 1, 3, 30));
        Assert.Equal(new DateOnly(2025, 11, 1), before);

        // 2026-11-01 08:00 UTC is Nov 1 morning Eastern.
        var after = DonationGoalProgressService.GetNovemberCycleStart(Utc(2026, 11, 1, 8));
        Assert.Equal(new DateOnly(2026, 11, 1), after);
    }

    private static DonationGoalProgressService CreateSut(List<DonationGoal> goals)
    {
        var repo = new Mock<IDonationGoalRepository>();
        repo.Setup(r => r.GetAllOrderedAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(goals);
        var uow = new Mock<IUnitOfWork>();
        uow.Setup(u => u.SaveChangesAsync(It.IsAny<CancellationToken>())).ReturnsAsync(0);
        return new DonationGoalProgressService(repo.Object, uow.Object);
    }

    private static List<DonationGoal> SeedGoals() =>
    [
        new DonationGoal
        {
            Id = 1,
            Key = DonationGoalKeys.LaunchAppStore,
            Title = "Launch on the app store!",
            Description = "Launch desc",
            TargetCents = 75_000,
            SortOrder = 1,
            Recurrence = DonationGoalRecurrence.OneTime
        },
        new DonationGoal
        {
            Id = 2,
            Key = DonationGoalKeys.LiveSixMonths,
            Title = "Live for six months!",
            Description = "Six months desc",
            TargetCents = 100_000,
            SortOrder = 2,
            Recurrence = DonationGoalRecurrence.OneTime
        },
        new DonationGoal
        {
            Id = 3,
            Key = DonationGoalKeys.LiveOneYear,
            Title = "Live for one year!",
            Description = "Year desc",
            TargetCents = 200_000,
            SortOrder = 3,
            Recurrence = DonationGoalRecurrence.AnnualNovember
        },
        new DonationGoal
        {
            Id = 4,
            Key = DonationGoalKeys.FundTeammate,
            Title = "Fund a teammate!",
            Description = "We believe in paying all of our employees a livable wage. {completions} teammates funded this year!",
            TargetCents = 13_000_000,
            SortOrder = 4,
            Recurrence = DonationGoalRecurrence.Repeating
        }
    ];

    private static void CompleteOneTimesAndAnnual(List<DonationGoal> goals)
    {
        goals[0].IsCompleted = true;
        goals[0].ProgressCents = goals[0].TargetCents;
        goals[1].IsCompleted = true;
        goals[1].ProgressCents = goals[1].TargetCents;
        goals[2].IsCompleted = true;
        goals[2].ProgressCents = goals[2].TargetCents;
        goals[2].AnnualYearStart = new DateOnly(2025, 11, 1);
    }

    private static DateTime Utc(int year, int month, int day, int hour = 15, int minute = 0) =>
        new(year, month, day, hour, minute, 0, DateTimeKind.Utc);
}
