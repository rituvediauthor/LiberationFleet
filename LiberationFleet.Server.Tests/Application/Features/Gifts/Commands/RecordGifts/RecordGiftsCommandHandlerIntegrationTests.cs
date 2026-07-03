using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.RecordGifts;

public class RecordGiftsCommandHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_WhenRecordingDirectGift_DoesNotUpdateCycleUntilRecipientVerifies()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var membershipRepository = new CrewMembershipRepository(fixture.Context);
        var giftRepository = new GiftRepository(fixture.Context);
        var crewPaymentPlatformRepository = new CrewPaymentPlatformRepository(fixture.Context);
        var recordHandler = new RecordGiftsCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            HandlerTestFixture.CreateNotificationService(fixture.Context),
            fixture.Context);

        var result = await recordHandler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    50,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Bob.Id,
                    null,
                    false,
                    "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var gift = await fixture.Context.Gifts.SingleAsync();
        gift.Type.Should().Be(GiftType.Direct);
        gift.VerificationStatus.Should().Be(GiftVerificationStatus.Pending);

        var cycleBeforeVerify = await fixture.Context.SeasonCycles.SingleAsync(c => c.UserId == fixture.Bob.Id);
        cycleBeforeVerify.CycleReceived.Should().Be(0m);

        var verifyHandler = new VerifyGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Bob.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            fixture.Service,
            fixture.Context);

        var verifyResult = await verifyHandler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);

        verifyResult.Success.Should().BeTrue();

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c => c.UserId == fixture.Bob.Id);
        cycle.CycleReceived.Should().Be(50m);
    }

    [Fact]
    public async Task Handle_WhenRecordingMiddlemanGift_CreatesInitiatedGiftWithoutUpdatingCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var membershipRepository = new CrewMembershipRepository(fixture.Context);
        var giftRepository = new GiftRepository(fixture.Context);
        var crewPaymentPlatformRepository = new CrewPaymentPlatformRepository(fixture.Context);
        var handler = new RecordGiftsCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Alice.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            HandlerTestFixture.CreateNotificationService(fixture.Context),
            fixture.Context);

        var result = await handler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    35,
                    fixture.Platforms["PayPal"].Id,
                    fixture.Bob.Id,
                    fixture.Carol.Id,
                    false,
                    "cycle")
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var gift = await fixture.Context.Gifts.SingleAsync();
        gift.Type.Should().Be(GiftType.Initiated);
        gift.MiddlemanUserId.Should().Be(fixture.Carol.Id);
        gift.CountsTowardReception.Should().BeFalse();

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c => c.UserId == fixture.Bob.Id);
        cycle.CycleReceived.Should().Be(0m);
    }

    [Fact]
    public async Task Handle_WhenRecordingSurvivalThresholdGift_AppliesThresholdAfterRecipientVerifies()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        await fixture.AddUnsatisfiedThresholdAsync(fixture.Bob, thresholdAmount: 75m);

        var membershipRepository = new CrewMembershipRepository(fixture.Context);
        var giftRepository = new GiftRepository(fixture.Context);
        var crewPaymentPlatformRepository = new CrewPaymentPlatformRepository(fixture.Context);
        var recordHandler = new RecordGiftsCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Carol.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            HandlerTestFixture.CreateNotificationService(fixture.Context),
            fixture.Context);

        var result = await recordHandler.Handle(
            new RecordGiftsCommand(
            [
                new GiftRecordItem(
                    20,
                    fixture.Platforms["Venmo"].Id,
                    fixture.Bob.Id,
                    null,
                    false,
                    "survivalThreshold")
            ]),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var gift = await fixture.Context.Gifts.SingleAsync();
        gift.IsSurvivalThreshold.Should().BeTrue();

        var thresholdBeforeVerify = await fixture.Context.MonthlySurvivalThresholds.SingleAsync();
        thresholdBeforeVerify.ReceivedAmount.Should().Be(0m);

        var verifyHandler = new VerifyGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Bob.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            fixture.Service,
            fixture.Context);

        var verifyResult = await verifyHandler.Handle(
            new VerifyGiftCommand(gift.Id, GiftVerificationAction.ConfirmReceived),
            CancellationToken.None);

        verifyResult.Success.Should().BeTrue();

        var threshold = await fixture.Context.MonthlySurvivalThresholds.SingleAsync();
        threshold.ReceivedAmount.Should().Be(20m);
    }
}
