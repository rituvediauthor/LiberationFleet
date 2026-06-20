using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewMembers;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetPendingMiddlemanGifts;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetNextAid;
using LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Gifts;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrder;
using LiberationFleet.Server.Domain.Enums;
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
    public async Task<IActionResult> GetLog(
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? beforeCreatedAt = null,
        [FromQuery] int? beforeId = null)
    {
        var result = await _mediator.Send(new GetCrewGiftLogQuery(limit, beforeCreatedAt, beforeId));
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

    [HttpGet("next-aid")]
    public async Task<IActionResult> GetNextAid()
    {
        var result = await _mediator.Send(new GetNextAidQuery());
        return Ok(result);
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
    public async Task<IActionResult> CompleteMiddlemanGift(int giftId, [FromBody] CompleteMiddlemanGiftRequest body)
    {
        var result = await _mediator.Send(new CompleteMiddlemanGiftCommand(giftId, body.PaymentPlatformId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{giftId:int}/verify")]
    public async Task<IActionResult> VerifyGift(int giftId, [FromBody] VerifyGiftRequest body)
    {
        if (!TryParseVerificationAction(body.Action, out var action))
        {
            return BadRequest(new GiftOperationResponse { Success = false, Message = "Invalid verification action." });
        }

        var result = await _mediator.Send(new VerifyGiftCommand(giftId, action, body.PaymentPlatformId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    private static bool TryParseVerificationAction(string action, out GiftVerificationAction parsed)
    {
        parsed = action switch
        {
            GiftVerificationUiHelper.ActionConfirmReceived => GiftVerificationAction.ConfirmReceived,
            GiftVerificationUiHelper.ActionConfirmNotReceived => GiftVerificationAction.ConfirmNotReceived,
            GiftVerificationUiHelper.ActionCompleteTransfer => GiftVerificationAction.CompleteTransfer,
            GiftVerificationUiHelper.ActionCantComplete => GiftVerificationAction.CantComplete,
            _ => default
        };

        return action is GiftVerificationUiHelper.ActionConfirmReceived
            or GiftVerificationUiHelper.ActionConfirmNotReceived
            or GiftVerificationUiHelper.ActionCompleteTransfer
            or GiftVerificationUiHelper.ActionCantComplete;
    }
}
