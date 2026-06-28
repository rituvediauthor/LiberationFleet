using LiberationFleet.Server.Application.Features.Notifications.Commands.MarkAllNotificationsRead;
using LiberationFleet.Server.Application.Features.Notifications.Commands.MarkNotificationRead;
using LiberationFleet.Server.Application.Features.Notifications.Commands.SetHiddenContent;
using LiberationFleet.Server.Application.Features.Notifications.Commands.SetMutedContent;
using LiberationFleet.Server.Application.Features.Notifications.Commands.UpdateNotificationPreferences;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Application.Features.Notifications.Queries.GetHiddenContent;
using LiberationFleet.Server.Application.Features.Notifications.Queries.GetMutedContent;
using LiberationFleet.Server.Application.Features.Notifications.Queries.GetNotificationPreferences;
using LiberationFleet.Server.Application.Features.Notifications.Queries.GetNotifications;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/notifications")]
[Authorize]
public class NotificationsController(IMediator mediator) : ControllerBase
{
    [HttpGet]
    public async Task<IActionResult> GetNotifications(
        [FromQuery] string? category = null,
        [FromQuery] int limit = 50,
        [FromQuery] int? beforeId = null)
    {
        var result = await mediator.Send(new GetNotificationsQuery(category, limit, beforeId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("preferences")]
    public async Task<IActionResult> GetPreferences()
    {
        var result = await mediator.Send(new GetNotificationPreferencesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("preferences")]
    public async Task<IActionResult> UpdatePreferences([FromBody] UpdateNotificationPreferencesRequest body)
    {
        var result = await mediator.Send(new UpdateNotificationPreferencesCommand(body.Preferences));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{notificationId:int}/read")]
    public async Task<IActionResult> MarkRead(int notificationId)
    {
        var result = await mediator.Send(new MarkNotificationReadCommand(notificationId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("read-all")]
    public async Task<IActionResult> MarkAllRead()
    {
        var result = await mediator.Send(new MarkAllNotificationsReadCommand());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("mutes")]
    public async Task<IActionResult> GetMutes()
    {
        var result = await mediator.Send(new GetMutedContentQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("mutes")]
    public async Task<IActionResult> SetMute([FromBody] SetMutedContentRequest body)
    {
        var result = await mediator.Send(new SetMutedContentCommand(body.ContentType, body.ResourceId, body.Muted));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("hidden")]
    public async Task<IActionResult> GetHidden()
    {
        var result = await mediator.Send(new GetHiddenContentQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("hidden")]
    public async Task<IActionResult> SetHidden([FromBody] SetHiddenContentRequest body)
    {
        var result = await mediator.Send(new SetHiddenContentCommand(body.ContentType, body.ResourceId, body.Hidden));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
