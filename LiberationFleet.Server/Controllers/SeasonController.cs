using LiberationFleet.Server.Application.Features.Season.Commands.ClearSeasonReady;
using LiberationFleet.Server.Application.Features.Season.Commands.MarkSeasonReady;
using LiberationFleet.Server.Application.Features.Season.Commands.ProposeStartSeason;
using LiberationFleet.Server.Application.Features.Season.Commands.SaveSeasonSetup;
using LiberationFleet.Server.Application.Features.Season.Queries.GetSeasonStatus;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class SeasonController : ControllerBase
{
    private readonly IMediator _mediator;

    public SeasonController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _mediator.Send(new GetSeasonStatusQuery());
        return Ok(result);
    }

    [HttpPost("ready")]
    public async Task<IActionResult> MarkReady([FromBody] MarkSeasonReadyCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("setup")]
    public async Task<IActionResult> SaveSetup([FromBody] SaveSeasonSetupCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("clear-ready")]
    public async Task<IActionResult> ClearReady()
    {
        var result = await _mediator.Send(new ClearSeasonReadyCommand());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    /// <summary>
    /// Creates a crew proposal to start the season once at least three members have marked ready.
    /// </summary>
    [HttpPost("propose-start")]
    public async Task<IActionResult> ProposeStart()
    {
        var result = await _mediator.Send(new ProposeStartSeasonCommand());
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
