using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetNextAid;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrder;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Gifts.Queries;

public class GetReceptionOrderQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsEmptyList()
    {
        var handler = new GetReceptionOrderQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(null).Object,
            HandlerTestFixture.CreateMutualAidServiceMock().Object);

        var result = await handler.Handle(new GetReceptionOrderQuery(), CancellationToken.None);

        result.Should().BeEmpty();
    }

    [Fact]
    public async Task Handle_DelegatesToMutualAidServiceWithSelfExclusion()
    {
        var mutualAidService = new Mock<IMutualAidService>(MockBehavior.Strict);
        mutualAidService
            .Setup(m => m.GetReceptionOrderAsync(
                5,
                10,
                true,
                true,
                true,
                It.IsAny<CancellationToken>()))
            .ReturnsAsync([]);

        var handler = new GetReceptionOrderQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(5).Object,
            mutualAidService.Object);

        await handler.Handle(new GetReceptionOrderQuery(10), CancellationToken.None);

        mutualAidService.VerifyAll();
    }
}

public class GetNextAidQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenUnauthorized_ReturnsNull()
    {
        var handler = new GetNextAidQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(null).Object,
            HandlerTestFixture.CreateMutualAidServiceMock().Object);

        var result = await handler.Handle(new GetNextAidQuery(), CancellationToken.None);

        result.Should().BeNull();
    }

    [Fact]
    public async Task Handle_DelegatesToMutualAidService()
    {
        var expected = new NextAidDto
        {
            RecipientName = "bob",
            Amount = 600m,
            IsCurrentUserRecipient = true,
            PlatformDisplayKind = NextAidPlatformDisplayKind.None
        };

        var mutualAidService = new Mock<IMutualAidService>(MockBehavior.Strict);
        mutualAidService
            .Setup(m => m.GetNextAidAsync(7, It.IsAny<CancellationToken>()))
            .ReturnsAsync(expected);

        var handler = new GetNextAidQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(7).Object,
            mutualAidService.Object);

        var result = await handler.Handle(new GetNextAidQuery(), CancellationToken.None);

        result.Should().BeSameAs(expected);
        mutualAidService.VerifyAll();
    }
}
