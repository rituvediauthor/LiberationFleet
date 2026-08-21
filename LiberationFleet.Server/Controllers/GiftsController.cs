using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGift;
using LiberationFleet.Server.Application.Features.Gifts.Commands.RecordGifts;
using LiberationFleet.Server.Application.Features.Gifts.Commands.CompleteMiddlemanGift;
using LiberationFleet.Server.Application.Features.Gifts.Commands.CreateGiftComment;
using LiberationFleet.Server.Application.Features.Gifts.Commands.ToggleGiftCommentLike;
using LiberationFleet.Server.Application.Features.Gifts.Commands.ToggleGiftLike;
using LiberationFleet.Server.Application.Features.Gifts.Commands.UpdateSeasonProfile;
using LiberationFleet.Server.Application.Features.Gifts.Commands.VerifyGift;
using LiberationFleet.Server.Application.Features.Gifts.Queries.ExportCrewGiftLog;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewGiftLog;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewMembers;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftCommentLikers;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftCommentReplies;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftDetail;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetGiftLikers;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetPendingMiddlemanGifts;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetMyGiftHistory;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetMyGiftHistoryForRecipient;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetNextAid;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetReceptionOrder;
using LiberationFleet.Server.Application.Features.Gifts.Queries.GetSeasonProfile;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Gifts;
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

    [HttpGet("my-history")]
    public async Task<IActionResult> GetMyGiftHistory()
    {
        var result = await _mediator.Send(new GetMyGiftHistoryQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("my-history/{recipientUserId:int}")]
    public async Task<IActionResult> GetMyGiftHistoryForRecipient(int recipientUserId)
    {
        var result = await _mediator.Send(new GetMyGiftHistoryForRecipientQuery(recipientUserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportLog()
    {
        var result = await _mediator.Send(new ExportCrewGiftLogQuery());
        return result.Success ? Ok(result) : BadRequest(result);
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

    [HttpGet("log/{giftId:int}")]
    public async Task<IActionResult> GetDetail(int giftId)
    {
        var result = await _mediator.Send(new GetGiftDetailQuery(giftId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("log/{giftId:int}/comments/{parentCommentId:int}/replies")]
    public async Task<IActionResult> GetCommentReplies(int giftId, int parentCommentId)
    {
        var result = await _mediator.Send(new GetGiftCommentRepliesQuery(giftId, parentCommentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("log/{giftId:int}/likers")]
    public async Task<IActionResult> GetGiftLikers(int giftId)
    {
        var result = await _mediator.Send(new GetGiftLikersQuery(giftId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("log/{giftId:int}/comments/{commentId:int}/likers")]
    public async Task<IActionResult> GetGiftCommentLikers(int giftId, int commentId)
    {
        var result = await _mediator.Send(new GetGiftCommentLikersQuery(giftId, commentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("log/{giftId:int}/comments")]
    public async Task<IActionResult> CreateComment(int giftId, [FromBody] CreateGiftCommentRequest body)
    {
        body ??= new CreateGiftCommentRequest();
        var result = await _mediator.Send(new CreateGiftCommentCommand(
            giftId,
            body.ParentCommentId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.MentionedUserIds,
            body.NotificationPreview));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("log/{giftId:int}/like")]
    public async Task<IActionResult> ToggleGiftLike(int giftId)
    {
        var result = await _mediator.Send(new ToggleGiftLikeCommand(giftId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("log/{giftId:int}/comments/{commentId:int}/like")]
    public async Task<IActionResult> ToggleGiftCommentLike(int giftId, int commentId)
    {
        var result = await _mediator.Send(new ToggleGiftCommentLikeCommand(giftId, commentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("season-profile")]
    public async Task<IActionResult> GetSeasonProfile()
    {
        var result = await _mediator.Send(new GetSeasonProfileQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("season-profile")]
    public async Task<IActionResult> UpdateSeasonProfile([FromBody] UpdateSeasonProfileCommand command)
    {
        var result = await _mediator.Send(command);
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
