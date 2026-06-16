using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;
using LiberationFleet.Server.Application.Features.Profile.Queries.GetMyProfile;
using LiberationFleet.Server.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LiberationFleet.Server.Tests.Controllers;

public class ProfileControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private ProfileController CreateController() => new(_mediator.Object);

    [Fact]
    public async Task Get_WhenProfileExists_ReturnsOk()
    {
        var profile = new UserProfileDto { Id = 1, Username = "user", Email = "user@example.com" };
        _mediator.Setup(m => m.Send(It.IsAny<GetMyProfileQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(profile);

        var result = await CreateController().Get();

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Get_WhenUnauthorized_ReturnsUnauthorized()
    {
        _mediator.Setup(m => m.Send(It.IsAny<GetMyProfileQuery>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync((UserProfileDto?)null);

        var result = await CreateController().Get();

        result.Should().BeOfType<UnauthorizedResult>();
    }

    [Fact]
    public async Task Update_WhenSuccessful_ReturnsOk()
    {
        var response = new ProfileOperationResponse { Success = true, Message = "Profile updated successfully" };
        _mediator.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Update(new UpdateProfileCommand());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Update_WhenFailed_ReturnsBadRequest()
    {
        var response = new ProfileOperationResponse { Success = false, Message = "Username is already taken" };
        _mediator.Setup(m => m.Send(It.IsAny<UpdateProfileCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Update(new UpdateProfileCommand());

        result.Should().BeOfType<BadRequestObjectResult>();
    }
}
