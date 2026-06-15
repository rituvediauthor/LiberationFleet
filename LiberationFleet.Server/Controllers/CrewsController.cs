using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;
using LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CrewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("membership")]
    public async Task<IActionResult> GetMembership()
    {
        var result = await _mediator.Send(new GetMyCrewMembershipQuery());
        return Ok(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCrewCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchCrewsQuery query)
    {
        var result = await _mediator.Send(query);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("join")]
    public async Task<IActionResult> Join([FromBody] JoinCrewCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
