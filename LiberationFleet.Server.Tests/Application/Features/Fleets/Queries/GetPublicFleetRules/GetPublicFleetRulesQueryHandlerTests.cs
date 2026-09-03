using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetPublicFleetRules;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Fleets.Queries.GetPublicFleetRules;

public class GetPublicFleetRulesQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenPrivateFleetAndMemberNeedsAcceptance_ReturnsPublicRules()
    {
        var fleet = new Fleet
        {
            Id = 12,
            Name = "Harbor Fleet",
            Privacy = CrewPrivacy.Private,
            JoinCode = "ABCD1234"
        };
        var rules = new List<FleetRule>
        {
            new()
            {
                Id = 3,
                FleetId = 12,
                IsPublic = true,
                Title = "Share openly",
                Description = "Public fleet rule body"
            }
        };

        var fleets = HandlerTestFixture.CreateFleetRepositoryMock();
        fleets.Setup(r => r.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(fleet);
        fleets.Setup(r => r.IsUserInFleetAsync(9, 12, It.IsAny<CancellationToken>())).ReturnsAsync(true);
        fleets.Setup(r => r.GetPublicRulesAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(rules);

        var handler = new GetPublicFleetRulesQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(9).Object,
            fleets.Object);

        var result = await handler.Handle(new GetPublicFleetRulesQuery(12, null), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.FleetId.Should().Be(12);
        result.Items.Should().ContainSingle();
        result.Items[0].Title.Should().Be("Share openly");
        result.Items[0].Description.Should().Be("Public fleet rule body");
    }

    [Fact]
    public async Task Handle_WhenPrivateFleetAndOutsiderWithoutJoinCode_HidesFleet()
    {
        var fleet = new Fleet
        {
            Id = 12,
            Name = "Harbor Fleet",
            Privacy = CrewPrivacy.Private,
            JoinCode = "ABCD1234"
        };

        var fleets = HandlerTestFixture.CreateFleetRepositoryMock();
        fleets.Setup(r => r.GetByIdAsync(12, It.IsAny<CancellationToken>())).ReturnsAsync(fleet);
        fleets.Setup(r => r.IsUserInFleetAsync(9, 12, It.IsAny<CancellationToken>())).ReturnsAsync(false);

        var handler = new GetPublicFleetRulesQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(9).Object,
            fleets.Object);

        var result = await handler.Handle(new GetPublicFleetRulesQuery(12, null), CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Fleet not found.");
        fleets.Verify(r => r.GetPublicRulesAsync(It.IsAny<int>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPublicFleet_ReturnsPlaintextPublicRulesForOutsider()
    {
        var fleet = new Fleet
        {
            Id = 4,
            Name = "Open Fleet",
            Privacy = CrewPrivacy.Public,
            JoinCode = "OPEN9999"
        };
        var rules = new List<FleetRule>
        {
            new()
            {
                Id = 1,
                FleetId = 4,
                IsPublic = true,
                Title = "Be kind",
                Description = "Plaintext public rule"
            }
        };

        var fleets = HandlerTestFixture.CreateFleetRepositoryMock();
        fleets.Setup(r => r.GetByIdAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(fleet);
        fleets.Setup(r => r.IsUserInFleetAsync(2, 4, It.IsAny<CancellationToken>())).ReturnsAsync(false);
        fleets.Setup(r => r.GetPublicRulesAsync(4, It.IsAny<CancellationToken>())).ReturnsAsync(rules);

        var handler = new GetPublicFleetRulesQueryHandler(
            HandlerTestFixture.CreateCurrentUserServiceMock(2).Object,
            fleets.Object);

        var result = await handler.Handle(new GetPublicFleetRulesQuery(4, null), CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Items.Should().ContainSingle(r => r.Title == "Be kind" && r.Description == "Plaintext public rule");
    }
}
