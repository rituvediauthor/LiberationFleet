using LiberationFleet.Server.Application.Features.Crewmates.Commands.AllowCrewmateRejoin;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.KickCrewmate;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.ManageFriendship;
using LiberationFleet.Server.Application.Features.Crewmates.Queries.GetKickedCrewmates;
using LiberationFleet.Server.Application.Features.Crewmates.Queries.GetCrewmateProfile;
using LiberationFleet.Server.Application.Features.Crewmates.Queries.GetCrewmates;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/crewmates")]
[Authorize]
public class CrewmatesController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrewmatesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("kicked")]
    public async Task<IActionResult> GetKicked()
    {
        var result = await _mediator.Send(new GetKickedCrewmatesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _mediator.Send(new GetCrewmatesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{userId:int}")]
    public async Task<IActionResult> GetProfile(int userId)
    {
        var result = await _mediator.Send(new GetCrewmateProfileQuery(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/friend-request")]
    public async Task<IActionResult> RequestFriendship(int userId)
    {
        var result = await _mediator.Send(new RequestFriendshipCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{userId:int}/friend-request")]
    public async Task<IActionResult> CancelFriendshipRequest(int userId)
    {
        var result = await _mediator.Send(new CancelFriendshipRequestCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/friend-request/accept")]
    public async Task<IActionResult> AcceptFriendship(int userId)
    {
        var result = await _mediator.Send(new AcceptFriendshipCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/friend-request/reject")]
    public async Task<IActionResult> RejectFriendship(int userId)
    {
        var result = await _mediator.Send(new RejectFriendshipCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{userId:int}/friendship")]
    public async Task<IActionResult> Unfriend(int userId)
    {
        var result = await _mediator.Send(new UnfriendCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/block")]
    public async Task<IActionResult> Block(int userId)
    {
        var result = await _mediator.Send(new BlockCrewmateCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/kick")]
    public async Task<IActionResult> Kick(int userId)
    {
        var result = await _mediator.Send(new KickCrewmateCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/allow-rejoin")]
    public async Task<IActionResult> AllowRejoin(int userId)
    {
        var result = await _mediator.Send(new AllowCrewmateRejoinCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
