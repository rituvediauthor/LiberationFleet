using LiberationFleet.Server.Application.Features.Fallible.Commands.RecordFallibleClick;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/fallible")]
public class FallibleController(IMediator mediator) : ControllerBase
{
    [HttpPost("click")]
    [AllowAnonymous]
    public async Task<IActionResult> RecordClick()
    {
        await mediator.Send(new RecordFallibleClickCommand());
        return NoContent();
    }
}
