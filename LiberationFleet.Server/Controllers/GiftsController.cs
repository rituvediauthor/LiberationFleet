using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewMembers;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetPendingMiddlemanGifts;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrder;
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

    [HttpGet("reception-order")]
    public async Task<IActionResult> GetReceptionOrder([FromQuery] int limit = 30)
    {
        var entries = await _mediator.Send(new GetReceptionOrderQuery(limit));
        return Ok(entries);
    }

    [HttpPost]
    public async Task<IActionResult> Record([FromBody] RecordGiftCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("batch")]
    public async Task<IActionResult> RecordBatch([FromBody] RecordGiftsCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{giftId:int}/complete")]
    public async Task<IActionResult> CompleteMiddlemanGift(int giftId)
    {
        var result = await _mediator.Send(new CompleteMiddlemanGiftCommand(giftId));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
