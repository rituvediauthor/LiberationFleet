using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Queries.GetEmergencyRequestDetail;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.EmergencyRequests.Queries;

public class GetEmergencyRequestDetailQueryTests
{
    [Fact]
    public async Task Handle_EligibleOfferer_LoadsDetailWithoutMutatingSeasonViaEnsure()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);

        var cycleCountBefore = fx.Context.SeasonCycles.Count();
        var handler = CreateHandler(fx, viewerUserId: fx.Bob.Id);

        var response = await handler.Handle(new GetEmergencyRequestDetailQuery(request.Id), CancellationToken.None);

        response.Success.Should().BeTrue(because: response.Message);
        response.Request.Should().NotBeNull();
        response.Request!.RequesterUsername.Should().Be("carol");
        response.Request.AmountNeeded.Should().Be(100m);
        response.Request.CanViewerSplitCycle.Should().BeTrue();
        response.Request.ViewerSplitMaxAmount.Should().Be(100m);

        // Detail GET must remain read-only for season cycle creation.
        fx.Context.SeasonCycles.Count().Should().Be(cycleCountBefore);
    }

    [Fact]
    public async Task Handle_RequesterViewingOwnRequest_LoadsSuccessfully()
    {
        await using var fx = await MutualAidSeasonFixture.CreateActiveSeasonAsync(cycleCap: 110m);
        var request = await AddEmergencyRequestAsync(fx, fx.Carol, amountNeeded: 100m);
        var handler = CreateHandler(fx, viewerUserId: fx.Carol.Id);

        var response = await handler.Handle(new GetEmergencyRequestDetailQuery(request.Id), CancellationToken.None);

        response.Success.Should().BeTrue(because: response.Message);
        response.Request.Should().NotBeNull();
        response.Request!.IsSelfRequest.Should().BeTrue();
        response.Request.CanViewerSplitCycle.Should().BeFalse();
    }

    private static GetEmergencyRequestDetailQueryHandler CreateHandler(
        MutualAidSeasonFixture fx,
        int viewerUserId)
    {
        var currentUser = new Mock<ICurrentUserService>();
        currentUser.SetupGet(c => c.UserId).Returns(viewerUserId);

        var membershipRepo = new CrewMembershipRepository(fx.Context);
        var emergencyRepo = new EmergencyRequestRepository(fx.Context);
        var fleetRepo = new FleetRepository(fx.Context);
        var mutualAidRepo = new MutualAidRepository(fx.Context);
        var splitService = new EmergencySplitService(
            mutualAidRepo,
            membershipRepo,
            emergencyRepo,
            fx.Service);

        return new GetEmergencyRequestDetailQueryHandler(
            currentUser.Object,
            membershipRepo,
            emergencyRepo,
            fleetRepo,
            mutualAidRepo,
            fx.Service,
            splitService);
    }

    private static async Task<EmergencyRequest> AddEmergencyRequestAsync(
        MutualAidSeasonFixture fx,
        User requester,
        decimal amountNeeded)
    {
        var request = new EmergencyRequest
        {
            CrewId = fx.Crew.Id,
            RequesterUserId = requester.Id,
            Purpose = "Detail load test",
            AmountNeeded = amountNeeded,
            AmountReceived = 0m,
            AmountSplitCommitted = 0m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            SplitEligibleOffererUserIds = EmergencySplitService.FormatEligibleOffererUserIds(
                [fx.Bob.Id, fx.Alice.Id])
        };
        fx.Context.EmergencyRequests.Add(request);
        await fx.Context.SaveChangesAsync();
        return request;
    }
}
