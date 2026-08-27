using LiberationFleet.Server.Application.Features.Chats.Commands.CreateChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Commands.CreateKickFromChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Commands.DeleteChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Commands.ReorderChatRooms;
using LiberationFleet.Server.Application.Features.Chats.Commands.SendChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Commands.ToggleAnonymousMode;
using LiberationFleet.Server.Application.Features.Chats.Commands.ToggleChatMessageLike;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetChatMessageLikers;
using LiberationFleet.Server.Application.Features.Chats.Commands.DeleteChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatMessage;
using LiberationFleet.Server.Application.Features.Chats.Commands.UpdateChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Contracts;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoom;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetChatRoomMessages;
using LiberationFleet.Server.Application.Features.Chats.Queries.GetCrewChatRooms;
using LiberationFleet.Server.Application.Features.Chats.Voice.Commands.DisconnectVoiceParticipant;
using LiberationFleet.Server.Application.Features.Chats.Voice.Commands.JoinVoiceRoom;
using LiberationFleet.Server.Application.Features.Chats.Voice.Commands.LeaveVoiceRoom;
using LiberationFleet.Server.Application.Features.Chats.Voice.Commands.ServerMuteVoiceParticipant;
using LiberationFleet.Server.Application.Features.Chats.Voice.Contracts;
using LiberationFleet.Server.Application.Features.Chats.Voice.Queries.GetVoicePresence;
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
        var isFleetScope = string.Equals(body.Scope, "fleet", StringComparison.OrdinalIgnoreCase);
        var result = await _mediator.Send(new CreateChatRoomCommand(
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.RoomType,
            body.Purpose,
            body.PlaintextName,
            body.IsAdultContent,
            isFleetScope));
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

    [HttpPut("rooms/order")]
    public async Task<IActionResult> ReorderRooms([FromBody] ReorderChatRoomsRequest body)
    {
        var result = await _mediator.Send(new ReorderChatRoomsCommand(
            body.RoomIds,
            body.Personal,
            body.Scope));
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

    [HttpPut("rooms/{roomId:int}/anonymous-mode")]
    public async Task<IActionResult> ToggleAnonymousMode(int roomId, [FromBody] ToggleAnonymousModeRequest body)
    {
        body ??= new ToggleAnonymousModeRequest();
        var result = await _mediator.Send(new ToggleAnonymousModeCommand(roomId, body.Enabled));
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
            body.KeyVersion,
            body.Body,
            body.MentionedUserIds,
            body.IsAnonymous));
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
            body.KeyVersion,
            body.Body,
            body.MentionedUserIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("rooms/{roomId:int}/messages/{messageId:int}")]
    public async Task<IActionResult> DeleteMessage(int roomId, int messageId)
    {
        var result = await _mediator.Send(new DeleteChatMessageCommand(roomId, messageId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/messages/{messageId:int}/like")]
    public async Task<IActionResult> ToggleMessageLike(int roomId, int messageId)
    {
        var result = await _mediator.Send(new ToggleChatMessageLikeCommand(roomId, messageId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("rooms/{roomId:int}/messages/{messageId:int}/likers")]
    public async Task<IActionResult> GetMessageLikers(int roomId, int messageId)
    {
        var result = await _mediator.Send(new GetChatMessageLikersQuery(roomId, messageId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/messages/{messageId:int}/kick")]
    public async Task<IActionResult> KickFromMessage(int roomId, int messageId, [FromBody] KickFromChatMessageRequest body)
    {
        body ??= new KickFromChatMessageRequest();
        var result = await _mediator.Send(new CreateKickFromChatMessageCommand(roomId, messageId, body.Reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/voice/join")]
    public async Task<IActionResult> JoinVoiceRoom(int roomId)
    {
        var result = await _mediator.Send(new JoinVoiceRoomCommand(roomId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/voice/leave")]
    public async Task<IActionResult> LeaveVoiceRoom(int roomId)
    {
        var result = await _mediator.Send(new LeaveVoiceRoomCommand(roomId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("voice/presence")]
    public async Task<IActionResult> GetVoicePresence([FromQuery] int crewId)
    {
        var result = await _mediator.Send(new GetVoicePresenceQuery(crewId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/voice/disconnect")]
    public async Task<IActionResult> DisconnectVoiceParticipant(int roomId, [FromBody] VoiceDisconnectRequest body)
    {
        var result = await _mediator.Send(new DisconnectVoiceParticipantCommand(roomId, body.UserId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("rooms/{roomId:int}/voice/server-mute")]
    public async Task<IActionResult> ServerMuteVoiceParticipant(int roomId, [FromBody] VoiceServerMuteRequest body)
    {
        var result = await _mediator.Send(new ServerMuteVoiceParticipantCommand(roomId, body.UserId, body.IsServerMuted));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
