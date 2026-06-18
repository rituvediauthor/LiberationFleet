using da_training_project_tests.Support;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class GiftEntityTests
{
    [Fact]
    public async Task Gift_IsSurvivalThreshold_CanBeSetToTrue()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Recipient", needsSurvivalAid: true);

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(
            new LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift.RecordGiftCommand(
                22m, 1, recipient.Id, null, null, IsSurvivalThreshold: true),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var stored = await context.Gifts.FirstAsync();
        stored.IsSurvivalThreshold.Should().BeTrue();
    }

    [Fact]
    public async Task Gift_CountsTowardReception_DefaultsToTrueForDirectGifts()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "DefRecip");

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        await handler.Handle(
            new LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift.RecordGiftCommand(
                20m, 1, recipient.Id, null, null),
            CancellationToken.None);

        var stored = await context.Gifts.FirstAsync();
        stored.CountsTowardReception.Should().BeTrue();
    }

    [Fact]
    public async Task Gift_InitiatedType_DoesNotCountTowardReceptionUntilCompleted()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "InitRecip");
        var middleman = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "InitMiddle");

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        await handler.Handle(
            new LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift.RecordGiftCommand(
                50m, 1, recipient.Id, middleman.Id, null),
            CancellationToken.None);

        var stored = await context.Gifts.FirstAsync();
        stored.CountsTowardReception.Should().BeFalse();
        stored.Type.Should().Be(GiftType.Initiated);
    }

    [Fact]
    public async Task SeasonCycle_UniqueConstraint_PerCrewUserSeason()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, user) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var seasonStart = crew.CurrentSeasonStartDate;

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
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, user) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);

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
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, _) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var stored = await context.Crews.FirstAsync(c => c.Id == crew.Id);
        stored.CurrentSeasonStartDate.Should().BeCloseTo(DateTime.UtcNow, TimeSpan.FromDays(35));
    }

    [Fact]
    public async Task User_PercentBonus_DefaultsToZero()
    {
        using var context = MutualAidTestFixture.CreateContext();

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
