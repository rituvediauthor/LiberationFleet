using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;
using LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;
using LiberationFleet.Server.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LiberationFleet.Server.Tests.Controllers;

public class CrewsControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private CrewsController CreateController() => new(_mediator.Object);

    [Fact]
    public async Task GetMembership_ReturnsOkWithStatus()
    {
        var status = new CrewMembershipStatusDto { HasCrew = true, CrewId = 1, CrewName = "Alpha" };
        _mediator.Setup(m => m.Send(It.IsAny<GetMyCrewMembershipQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(status);

        var result = await CreateController().GetMembership();

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(status);
    }

    [Fact]
    public async Task Create_WhenSuccessful_ReturnsOk()
    {
        var response = new CrewOperationResponse { Success = true, Message = "Crew created successfully" };
        _mediator.Setup(m => m.Send(It.IsAny<CreateCrewCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Create(new CreateCrewCommand());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Create_WhenFailed_ReturnsBadRequest()
    {
        var response = new CrewOperationResponse { Success = false, Message = "You are already a member of a crew" };
        _mediator.Setup(m => m.Send(It.IsAny<CreateCrewCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Create(new CreateCrewCommand());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Search_WhenSuccessful_ReturnsOk()
    {
        var response = new CrewSearchResponse { Success = true, Items = [] };
        _mediator.Setup(m => m.Send(It.IsAny<SearchCrewsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Search(new SearchCrewsQuery());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Search_WhenUnauthorized_ReturnsBadRequest()
    {
        var response = new CrewSearchResponse { Success = false, Message = "Unauthorized" };
        _mediator.Setup(m => m.Send(It.IsAny<SearchCrewsQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Search(new SearchCrewsQuery());

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
