using LiberationFleet.Server.Application.Features.Friends.Commands.SendDirectMessage;
using LiberationFleet.Server.Application.Features.Friends.Commands.UpdateDirectMessage;
using LiberationFleet.Server.Application.Features.Friends.Contracts;
using LiberationFleet.Server.Application.Features.Friends.Queries.GetBlockedUsers;
using LiberationFleet.Server.Application.Features.Friends.Queries.GetDirectMessages;
using LiberationFleet.Server.Application.Features.Friends.Queries.GetFriendRequests;
using LiberationFleet.Server.Application.Features.Friends.Queries.GetFriends;
using LiberationFleet.Server.Application.Features.Friends.Queries.SearchUsers;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/friends")]
[Authorize]
public class FriendsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FriendsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetFriends([FromQuery] string? search)
    {
        var result = await _mediator.Send(new GetFriendsQuery(search));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("requests")]
    public async Task<IActionResult> GetRequests()
    {
        var result = await _mediator.Send(new GetFriendRequestsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("blocked")]
    public async Task<IActionResult> GetBlocked()
    {
        var result = await _mediator.Send(new GetBlockedUsersQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("search")]
    public async Task<IActionResult> Search([FromQuery] string username)
    {
        var result = await _mediator.Send(new SearchUsersQuery(username));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("messages/{friendUserId:int}")]
    public async Task<IActionResult> GetMessages(
        int friendUserId,
        [FromQuery] int limit = 50,
        [FromQuery] int? beforeMessageId = null)
    {
        var result = await _mediator.Send(new GetDirectMessagesQuery(friendUserId, limit, beforeMessageId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("messages/{friendUserId:int}")]
    public async Task<IActionResult> SendMessage(int friendUserId, [FromBody] SendDirectMessageRequest body)
    {
        body ??= new SendDirectMessageRequest();
        var result = await _mediator.Send(new SendDirectMessageCommand(
            friendUserId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("messages/{friendUserId:int}/{messageId:int}")]
    public async Task<IActionResult> UpdateMessage(int friendUserId, int messageId, [FromBody] UpdateDirectMessageRequest body)
    {
        body ??= new UpdateDirectMessageRequest();
        var result = await _mediator.Send(new UpdateDirectMessageCommand(
            friendUserId,
            messageId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
