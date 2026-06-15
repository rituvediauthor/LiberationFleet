using LiberationFleet.Server.Application.Features.Auth.Commands.Login;
using LiberationFleet.Server.Application.Features.Auth.Commands.Register;
using LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;
using LiberationFleet.Server.Application.Features.Auth.Commands.ResetPassword;
using LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;
using MediatR;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
public class AuthController : ControllerBase
{
    private readonly IMediator _mediator;

    public AuthController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpPost("register")]
    public async Task<IActionResult> Register([FromBody] RegisterCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("login")]
    public async Task<IActionResult> Login([FromBody] LoginCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : Unauthorized(result);
    }

    [HttpPost("request-password-reset")]
    public async Task<IActionResult> RequestPasswordReset([FromBody] RequestPasswordResetCommand command)
    {
        var result = await _mediator.Send(command);
        return Ok(result);
    }

    [HttpPost("validate-reset-token")]
    public async Task<IActionResult> ValidateResetToken([FromBody] ValidateResetTokenQuery query)
    {
        var result = await _mediator.Send(query);
        return Ok(result);
    }

    [HttpPost("reset-password")]
    public async Task<IActionResult> ResetPassword([FromBody] ResetPasswordCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
