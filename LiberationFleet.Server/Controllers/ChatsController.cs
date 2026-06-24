using LiberationFleet.Server.Application.Features.Chats.Commands.CreateChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Commands.SendChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoomMessages;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetCrewChatRooms;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/chats")]
[Authorize]
public class ChatsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ChatsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("rooms")]
    public async Task<IActionResult> GetRooms()
    {
        var result = await _mediator.Send(new GetCrewChatRoomsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateChatRoomRequest body)
    {
        var result = await _mediator.Send(new CreateChatRoomCommand(
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.RoomType));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("rooms/{roomId:int}/messages")]
    public async Task<IActionResult> GetMessages(int roomId, [FromQuery] int limit = 50, [FromQuery] int? beforeMessageId = null)
    {
        var result = await _mediator.Send(new GetChatRoomMessagesQuery(roomId, limit, beforeMessageId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/messages")]
    public async Task<IActionResult> SendMessage(int roomId, [FromBody] SendChatMessageRequest body)
    {
        var result = await _mediator.Send(new SendChatMessageCommand(
            roomId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
