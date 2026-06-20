using LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Commands.CompleteMiddlemanGift;

public class CompleteMiddlemanGiftCommandHandlerIntegrationTests
{
    [Fact]
    public async Task Handle_WhenMiddlemanCompletesGift_PersistsCompletedGiftAndUpdatesCycle()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var initiated = new Domain.Entities.Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            MiddlemanUserId = fixture.Carol.Id,
            Type = GiftType.Initiated,
            Amount = 40m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CountsTowardReception = false,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.Gifts.Add(initiated);
        await fixture.Context.SaveChangesAsync();

        var membershipRepository = new CrewMembershipRepository(fixture.Context);
        var giftRepository = new GiftRepository(fixture.Context);
        var crewPaymentPlatformRepository = new CrewPaymentPlatformRepository(fixture.Context);
        var handler = new CompleteMiddlemanGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Carol.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            fixture.Service,
            fixture.Context);

        var result = await handler.Handle(
            new CompleteMiddlemanGiftCommand(initiated.Id, fixture.Platforms["Venmo"].Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();

        var completed = await fixture.Context.Gifts
            .Where(g => g.Type == GiftType.Completed)
            .SingleAsync();

        completed.InitiatedGiftId.Should().Be(initiated.Id);
        completed.CrewPaymentPlatformId.Should().Be(fixture.Platforms["Venmo"].Id);
        completed.CountsTowardReception.Should().BeTrue();

        var cycle = await fixture.Context.SeasonCycles.SingleAsync(c => c.UserId == fixture.Bob.Id);
        cycle.CycleReceived.Should().Be(40m);
    }

    [Fact]
    public async Task Handle_WhenPlatformNotSharedWithRecipient_ReturnsFailure()
    {
        await using var fixture = await MutualAidSeasonFixture.CreateActiveSeasonAsync();

        var initiated = new Domain.Entities.Gift
        {
            CrewId = fixture.Crew.Id,
            GiverUserId = fixture.Alice.Id,
            RecipientUserId = fixture.Bob.Id,
            MiddlemanUserId = fixture.Carol.Id,
            Type = GiftType.Initiated,
            Amount = 40m,
            CrewPaymentPlatformId = fixture.Platforms["PayPal"].Id,
            CreatedAt = DateTime.UtcNow
        };
        fixture.Context.Gifts.Add(initiated);
        await fixture.Context.SaveChangesAsync();

        var membershipRepository = new CrewMembershipRepository(fixture.Context);
        var giftRepository = new GiftRepository(fixture.Context);
        var crewPaymentPlatformRepository = new CrewPaymentPlatformRepository(fixture.Context);
        var handler = new CompleteMiddlemanGiftCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fixture.Carol.Id).Object,
            membershipRepository,
            giftRepository,
            crewPaymentPlatformRepository,
            fixture.Service,
            fixture.Context);

        var result = await handler.Handle(
            new CompleteMiddlemanGiftCommand(initiated.Id, fixture.Platforms["Zelle"].Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Selected payment platform is not shared with the recipient.");
        (await fixture.Context.Gifts.CountAsync(g => g.Type == GiftType.Completed)).Should().Be(0);
    }
}
