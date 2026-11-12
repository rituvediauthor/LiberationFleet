using LiberationFleet.Server.Application.Features.AccountRecovery.Commands.BeginAccountRecovery;
using LiberationFleet.Server.Application.Features.AccountRecovery.Commands.CompleteAccountRecovery;
using LiberationFleet.Server.Application.Features.AccountRecovery.Commands.ResendAccountRecovery;
using LiberationFleet.Server.Application.Features.AccountRecovery.Contracts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/account-recovery")]
[Authorize]
public class AccountRecoveryController(IMediator mediator) : ControllerBase
{
    [HttpPost("begin")]
    public async Task<IActionResult> Begin([FromBody] BeginAccountRecoveryRequest body)
    {
        var result = await mediator.Send(new BeginAccountRecoveryCommand(body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("resend")]
    public async Task<IActionResult> Resend([FromBody] ResendAccountRecoveryRequest body)
    {
        var result = await mediator.Send(new ResendAccountRecoveryCommand(body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("complete")]
    public async Task<IActionResult> Complete([FromBody] CompleteAccountRecoveryRequest body)
    {
        var result = await mediator.Send(new CompleteAccountRecoveryCommand(body));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
