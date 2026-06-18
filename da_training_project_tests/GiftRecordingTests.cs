using da_training_project_tests.Support;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.EntityFrameworkCore;

namespace da_training_project_tests;

public class GiftRecordingTests
{
    [Fact]
    public async Task RecordGift_NoAmountRestriction_AllowsGiftExceedingNeedAmount()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Dave", emergencyLevel: 2);

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(new RecordGiftCommand(25m, 1, recipient.Id, null, null), CancellationToken.None);

        result.Success.Should().BeTrue();
        var gift = await context.Gifts.FirstAsync(g => g.RecipientUserId == recipient.Id);
        gift.Amount.Should().Be(25m);
    }

    [Fact]
    public async Task RecordGift_DirectGift_SharedPaymentPlatform()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "RecipientB");

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "giver@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 1, Handle = "recipient@paypal" });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(new RecordGiftCommand(30m, 1, recipient.Id, null, null), CancellationToken.None);

        result.Success.Should().BeTrue();
        var gift = await context.Gifts.FirstAsync();
        gift.Type.Should().Be(GiftType.Direct);
        gift.MiddlemanUserId.Should().BeNull();
    }

    [Fact]
    public async Task RecordGift_MiddlemanGift_InitiatedCorrectly()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "CrewmateB");
        var middleman = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "CrewmateC");

        context.UserPaymentPlatforms.AddRange(
            new UserPaymentPlatform { UserId = giver.Id, PaymentPlatformId = 1, Handle = "a@paypal" },
            new UserPaymentPlatform { UserId = recipient.Id, PaymentPlatformId = 2, Handle = "b@cashapp" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = 1, Handle = "c@paypal" },
            new UserPaymentPlatform { UserId = middleman.Id, PaymentPlatformId = 2, Handle = "c@cashapp" });
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(new RecordGiftCommand(30m, 1, recipient.Id, middleman.Id, null), CancellationToken.None);

        result.Success.Should().BeTrue();
        var gift = await context.Gifts.FirstAsync();
        gift.Type.Should().Be(GiftType.Initiated);
        gift.MiddlemanUserId.Should().Be(middleman.Id);
        gift.GiverUserId.Should().Be(giver.Id);
        gift.RecipientUserId.Should().Be(recipient.Id);
    }

    [Fact]
    public async Task RecordGift_MiddlemanCompletes_MarksAsCompleted()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Recipient");
        var middleman = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "Middleman");

        var initiated = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middleman.Id,
            Type = GiftType.Initiated,
            Amount = 30m,
            PaymentPlatformId = 1,
            CreatedAt = DateTime.UtcNow,
            CountsTowardReception = false
        };
        context.Gifts.Add(initiated);
        await context.SaveChangesAsync();

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, middleman.Id);
        var result = await handler.Handle(new RecordGiftCommand(30m, 2, null, null, initiated.Id), CancellationToken.None);

        result.Success.Should().BeTrue();

        var completed = await context.Gifts.Where(g => g.Type == GiftType.Completed).SingleAsync();
        completed.InitiatedGiftId.Should().Be(initiated.Id);
        completed.MiddlemanUserId.Should().Be(middleman.Id);
        completed.Amount.Should().Be(30m);
    }

    [Fact]
    public async Task RecordGift_InitiatedGift_CountsAsContributionImmediately()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "ImmRecip");
        var middleman = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "ImmMiddle");

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(new RecordGiftCommand(50m, 1, recipient.Id, middleman.Id, null), CancellationToken.None);

        result.Success.Should().BeTrue();

        var calculationService = MutualAidTestFixture.CreateCalculationService(context);
        var monthlyCapacity = await calculationService.GetUserMonthlyGivingCapacityAsync(giver.Id, crew.Id);
        monthlyCapacity.Should().BeApproximately(50m / 3m, 0.01m);
    }

    [Fact]
    public async Task RecordGift_CompletedGift_CountsTowardRecipientReception()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "RRecip");
        var middleman = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "RMiddle");

        context.SeasonCycles.Add(new SeasonCycle
        {
            CrewId = crew.Id,
            UserId = recipient.Id,
            SeasonStartDate = crew.CurrentSeasonStartDate,
            CycleCapAtStart = 500m,
            TotalReceptionAmount = 0m,
            SurvivalThresholdReceived = 0m,
            CycleReceived = 0m,
            CycleCompleted = false,
            PriorityScoreAtSeasonStart = 100m,
            ReceptionOrderPosition = 1
        });
        await context.SaveChangesAsync();

        var initiated = new Gift
        {
            CrewId = crew.Id,
            GiverUserId = giver.Id,
            RecipientUserId = recipient.Id,
            MiddlemanUserId = middleman.Id,
            Type = GiftType.Initiated,
            Amount = 40m,
            PaymentPlatformId = 1,
            CreatedAt = DateTime.UtcNow,
            CountsTowardReception = false
        };
        context.Gifts.Add(initiated);
        await context.SaveChangesAsync();

        var cycleBefore = await context.SeasonCycles.SingleAsync(s => s.UserId == recipient.Id);
        cycleBefore.CycleReceived.Should().Be(0m);

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, middleman.Id);
        var result = await handler.Handle(new RecordGiftCommand(40m, 2, null, null, initiated.Id), CancellationToken.None);

        result.Success.Should().BeTrue();

        var cycleAfter = await context.SeasonCycles.SingleAsync(s => s.UserId == recipient.Id);
        cycleAfter.CycleReceived.Should().Be(40m);
    }

    [Fact]
    public async Task RecordGift_SelfGift_ReturnsFailure()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, user) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, user.Id);
        var result = await handler.Handle(new RecordGiftCommand(20m, 1, user.Id, null, null), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You cannot give a gift to yourself.");
    }

    [Fact]
    public async Task RecordGift_CannotBeMiddlemanForOwnGift()
    {
        using var context = MutualAidTestFixture.CreateContext();
        await MutualAidTestFixture.SeedPaymentPlatformsAsync(context);

        var (crew, giver) = await MutualAidTestFixture.SeedCrewWithCreatorAsync(context);
        var recipient = await MutualAidTestFixture.AddCrewMemberAsync(context, crew, "OwnRecip");

        var handler = MutualAidTestFixture.CreateRecordGiftHandler(context, giver.Id);
        var result = await handler.Handle(new RecordGiftCommand(30m, 1, recipient.Id, giver.Id, null), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("You cannot be the middleman for your own gift.");
    }
}
