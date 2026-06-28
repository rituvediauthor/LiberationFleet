using LiberationFleet.Server.Application.Features.Chats.Commands.CreateChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Commands.DeleteChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Commands.SendChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoom;
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

    [HttpGet("rooms/{roomId:int}")]
    public async Task<IActionResult> GetRoom(int roomId)
    {
        var result = await _mediator.Send(new GetChatRoomQuery(roomId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms")]
    public async Task<IActionResult> CreateRoom([FromBody] CreateChatRoomRequest body)
    {
        var result = await _mediator.Send(new CreateChatRoomCommand(
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.RoomType,
            body.Purpose,
            body.PlaintextName));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("rooms/{roomId:int}")]
    public async Task<IActionResult> UpdateRoom(int roomId, [FromBody] UpdateChatRoomRequest body)
    {
        var result = await _mediator.Send(new UpdateChatRoomCommand(
            roomId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.RoomType,
            body.Purpose,
            body.PlaintextName,
            body.PlaintextOldName,
            body.PlaintextOldPurpose));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("rooms/{roomId:int}")]
    public async Task<IActionResult> DeleteRoom(int roomId, [FromBody] DeleteChatRoomRequest? body)
    {
        body ??= new DeleteChatRoomRequest();
        var result = await _mediator.Send(new DeleteChatRoomCommand(
            roomId,
            body.PlaintextName,
            body.PlaintextPurpose));
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

    [HttpPut("rooms/{roomId:int}/messages/{messageId:int}")]
    public async Task<IActionResult> UpdateMessage(int roomId, int messageId, [FromBody] UpdateChatMessageRequest body)
    {
        body ??= new UpdateChatMessageRequest();
        var result = await _mediator.Send(new UpdateChatMessageCommand(
            roomId,
            messageId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
