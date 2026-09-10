using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.CreateEmergencyRequest;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests.Commands;

public class CreateEmergencyRequestCommandTests
{
    [Fact]
    public async Task Handle_WhenInSeason_CreatesRequestAndCapturesLockedEligibleOfferers()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);

        var handler = new CreateEmergencyRequestCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fx.Carol.Id).Object,
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
            new UserRepository(fx.Context),
            new EmergencySplitService(
                new MutualAidRepository(fx.Context),
                new CrewMembershipRepository(fx.Context),
                new EmergencyRequestRepository(fx.Context),
                fx.Service,
                fx.Context),
            HandlerTestFixture.CreateNotificationService(fx.Context),
            fx.Context);

        var result = await handler.Handle(
            new CreateEmergencyRequestCommand("Car repair", 42.25m),
            CancellationToken.None);

        result.Success.Should().BeTrue(result.Message);
        result.RequestId.Should().BeGreaterThan(0);

        var saved = await fx.Context.EmergencyRequests.SingleAsync(r => r.Id == result.RequestId);
        saved.Purpose.Should().Be("Car repair");
        saved.AmountNeeded.Should().Be(43m); // ceiling to whole dollar
        saved.AmountReceived.Should().Be(0m);
        saved.AmountSplitCommitted.Should().Be(0m);
        saved.SplitEligibleOffererUserIds.Should().NotBeNull();
    }

    [Fact]
    public async Task Handle_WhenNotInSeason_Rejects()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var membership = await fx.Context.CrewMemberships.SingleAsync(m => m.UserId == fx.Carol.Id);
        membership.IsInSeason = false;
        await fx.Context.SaveChangesAsync();

        var handler = new CreateEmergencyRequestCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fx.Carol.Id).Object,
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
            new UserRepository(fx.Context),
            new EmergencySplitService(
                new MutualAidRepository(fx.Context),
                new CrewMembershipRepository(fx.Context),
                new EmergencyRequestRepository(fx.Context),
                fx.Service,
                fx.Context),
            HandlerTestFixture.CreateNotificationService(fx.Context),
            fx.Context);

        var result = await handler.Handle(
            new CreateEmergencyRequestCommand("Need help", 20m),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("active season");
    }
}
