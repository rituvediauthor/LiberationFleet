using LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.CancelEmergencyRequest;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests.Commands;

public class CancelEmergencyRequestCommandTests
{
    [Fact]
    public async Task Handle_WhenRequesterClosesOpenRequest_SetsCancelledAndKeepsSplits()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 100m);
        var request = new EmergencyRequest
        {
            CrewId = fx.Crew.Id,
            RequesterUserId = fx.Carol.Id,
            Purpose = "Close me",
            AmountNeeded = 50m,
            AmountReceived = 0m,
            AmountSplitCommitted = 0m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            SplitEligibleOffererUserIds = EmergencySplitService.FormatEligibleOffererUserIds(
                [fx.Bob.Id, fx.Alice.Id])
        };
        fx.Context.EmergencyRequests.Add(request);
        await fx.Context.SaveChangesAsync();

        var splitService = new EmergencySplitService(
            new MutualAidRepository(fx.Context),
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
                fx.Service,
                fx.Context);
        (await splitService.ApplySplitAsync(request, fx.Alice.Id, 25m, CancellationToken.None))
            .Success.Should().BeTrue();
        await fx.Context.SaveChangesAsync();

        var handler = new CancelEmergencyRequestCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fx.Carol.Id).Object,
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
            fx.Context);

        var result = await handler.Handle(
            new CancelEmergencyRequestCommand(request.Id),
            CancellationToken.None);

        result.Success.Should().BeTrue();
        var reloaded = await fx.Context.EmergencyRequests.SingleAsync(r => r.Id == request.Id);
        reloaded.Status.Should().Be(EmergencyRequestStatus.Cancelled);
        reloaded.AmountSplitCommitted.Should().Be(25m);

        var emergencySegment = await fx.Context.SeasonCycles.SingleAsync(c =>
            c.EmergencyRequestId == request.Id && !c.CycleCompleted);
        emergencySegment.CycleCapAtStart.Should().Be(25m);

        var openList = await new EmergencyRequestRepository(fx.Context)
            .GetOpenByCrewIdAsync(fx.Crew.Id, CancellationToken.None);
        openList.Should().NotContain(r => r.Id == request.Id);
    }

    [Fact]
    public async Task Handle_WhenNotRequester_Rejects()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync();
        var request = new EmergencyRequest
        {
            CrewId = fx.Crew.Id,
            RequesterUserId = fx.Carol.Id,
            Purpose = "Not yours",
            AmountNeeded = 40m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow
        };
        fx.Context.EmergencyRequests.Add(request);
        await fx.Context.SaveChangesAsync();

        var handler = new CancelEmergencyRequestCommandHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(fx.Alice.Id).Object,
            new CrewMembershipRepository(fx.Context),
            new EmergencyRequestRepository(fx.Context),
            fx.Context);

        var result = await handler.Handle(
            new CancelEmergencyRequestCommand(request.Id),
            CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Contain("requester");
        (await fx.Context.EmergencyRequests.SingleAsync(r => r.Id == request.Id))
            .Status.Should().Be(EmergencyRequestStatus.Open);
    }
}
