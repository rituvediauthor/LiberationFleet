using LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.CreateEmergencyRequest;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.MarkEmergencyGiftAlreadyLogged;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.RecordEmergencyGift;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.SubmitEmergencySplit;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Queries.GetEmergencyRequestDetail;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Queries.GetEmergencyRequests;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/emergency-requests")]
[Authorize]
public class EmergencyRequestsController : ControllerBase
{
    private readonly IMediator _mediator;

    public EmergencyRequestsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _mediator.Send(new GetEmergencyRequestsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _mediator.Send(new GetEmergencyRequestDetailQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateEmergencyRequestRequest body)
    {
        body ??= new CreateEmergencyRequestRequest();
        var result = await _mediator.Send(new CreateEmergencyRequestCommand(body.Purpose, body.AmountNeeded));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/record-gift")]
    public async Task<IActionResult> RecordGift(int id, [FromBody] RecordEmergencyGiftRequest body)
    {
        body ??= new RecordEmergencyGiftRequest();
        var result = await _mediator.Send(new RecordEmergencyGiftCommand(
            id,
            body.Amount,
            body.PaymentPlatformId,
            body.MiddlemanId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/already-logged")]
    public async Task<IActionResult> MarkAlreadyLogged(int id, [FromBody] RecordEmergencyGiftRequest body)
    {
        body ??= new RecordEmergencyGiftRequest();
        var result = await _mediator.Send(new MarkEmergencyGiftAlreadyLoggedCommand(id, body.Amount));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/split-cycle")]
    public async Task<IActionResult> SplitCycle(int id, [FromBody] SubmitEmergencySplitRequest body)
    {
        body ??= new SubmitEmergencySplitRequest();
        var result = await _mediator.Send(new SubmitEmergencySplitCommand(id, body.Amount));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
