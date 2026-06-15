using LiberationFleet.Server.Application.Features.Auth.Commands.Login;
using LiberationFleet.Server.Application.Features.Auth.Commands.Register;
using LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;
using LiberationFleet.Server.Application.Features.Auth.Commands.ResetPassword;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;
using LiberationFleet.Server.Controllers;
using MediatR;
using Microsoft.AspNetCore.Mvc;
using Moq;

namespace LiberationFleet.Server.Tests.Controllers;

public class AuthControllerTests
{
    private readonly Mock<IMediator> _mediator = new();

    private AuthController CreateController() => new(_mediator.Object);

    [Fact]
    public async Task Register_WhenSuccessful_ReturnsOk()
    {
        var response = new LoginResponse { Success = true, Message = "Registration successful" };
        _mediator.Setup(m => m.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Register(new RegisterCommand());

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(response);
    }

    [Fact]
    public async Task Register_WhenFailed_ReturnsBadRequest()
    {
        var response = new LoginResponse { Success = false, Message = "Email or username already registered" };
        _mediator.Setup(m => m.Send(It.IsAny<RegisterCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Register(new RegisterCommand());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Login_WhenSuccessful_ReturnsOk()
    {
        var response = new LoginResponse { Success = true, Message = "Login successful" };
        _mediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Login(new LoginCommand());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task Login_WhenFailed_ReturnsUnauthorized()
    {
        var response = new LoginResponse { Success = false, Message = "Invalid credentials" };
        _mediator.Setup(m => m.Send(It.IsAny<LoginCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().Login(new LoginCommand());

        result.Should().BeOfType<UnauthorizedObjectResult>();
    }

    [Fact]
    public async Task RequestPasswordReset_AlwaysReturnsOk()
    {
        var response = new PasswordResetResponse { Success = true, Message = "If the email is in our system..." };
        _mediator.Setup(m => m.Send(It.IsAny<RequestPasswordResetCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().RequestPasswordReset(new RequestPasswordResetCommand());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ValidateResetToken_ReturnsOkWithValidationResult()
    {
        var response = new ValidateResetTokenResponse { IsValid = true, Message = "Token is valid" };
        _mediator.Setup(m => m.Send(It.IsAny<ValidateResetTokenQuery>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().ValidateResetToken(new ValidateResetTokenQuery());

        result.Should().BeOfType<OkObjectResult>();
        ((OkObjectResult)result).Value.Should().Be(response);
    }

    [Fact]
    public async Task ResetPassword_WhenSuccessful_ReturnsOk()
    {
        var response = new PasswordResetResponse { Success = true, Message = "Password reset successfully" };
        _mediator.Setup(m => m.Send(It.IsAny<ResetPasswordCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().ResetPassword(new ResetPasswordCommand());

        result.Should().BeOfType<OkObjectResult>();
    }

    [Fact]
    public async Task ResetPassword_WhenFailed_ReturnsBadRequest()
    {
        var response = new PasswordResetResponse { Success = false, Message = "Invalid or expired reset token" };
        _mediator.Setup(m => m.Send(It.IsAny<ResetPasswordCommand>(), It.IsAny<CancellationToken>())).ReturnsAsync(response);

        var result = await CreateController().ResetPassword(new ResetPasswordCommand());

        result.Should().BeOfType<BadRequestObjectResult>();
    }

    [Fact]
    public async Task Register_SendsCommandToMediator()
    {
        var command = new RegisterCommand { Username = "user", Email = "user@example.com" };
        _mediator.Setup(m => m.Send(command, It.IsAny<CancellationToken>()))
            .ReturnsAsync(new LoginResponse { Success = true });

        await CreateController().Register(command);

        _mediator.Verify(m => m.Send(command, It.IsAny<CancellationToken>()), Times.Once);
    }
}
