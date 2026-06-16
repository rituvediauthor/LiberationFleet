using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewMembers;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetPendingMiddlemanGifts;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class GiftsController : ControllerBase
{
    private readonly IMediator _mediator;

    public GiftsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("log")]
    public async Task<IActionResult> GetLog()
    {
        var result = await _mediator.Send(new GetCrewGiftLogQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("members")]
    public async Task<IActionResult> GetMembers()
    {
        var members = await _mediator.Send(new GetCrewMembersQuery());
        return Ok(members);
    }

    [HttpGet("pending-middleman")]
    public async Task<IActionResult> GetPendingMiddlemanGifts()
    {
        var gifts = await _mediator.Send(new GetPendingMiddlemanGiftsQuery());
        return Ok(gifts);
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordGiftCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
