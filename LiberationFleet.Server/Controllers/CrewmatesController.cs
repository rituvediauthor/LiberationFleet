using LiberationFleet.Server.Application.Features.Crewmates.Commands.AddPlaceholderCrewmate;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.AllowCrewmateRejoin;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.ClaimPlaceholderIdentity;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.DemoteCrewRoles;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.KickCrewmate;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.ManageFriendship;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.NominateCrewRoles;
using LiberationFleet.Server.Application.Features.Crewmates.Commands.ToggleCanAttachFiles;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crewmates.Queries.ExportCrewmateStates;
using LiberationFleet.Server.Application.Features.Crewmates.Queries.GetCrewRoleDefinitions;
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

    [HttpGet("roles")]
    public async Task<IActionResult> GetRoleDefinitions()
    {
        var result = await _mediator.Send(new GetCrewRoleDefinitionsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("export-states")]
    public async Task<IActionResult> ExportStates()
    {
        var result = await _mediator.Send(new ExportCrewmateStatesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("placeholders")]
    public async Task<IActionResult> AddPlaceholder([FromBody] AddPlaceholderCrewmateRequest body)
    {
        body ??= new AddPlaceholderCrewmateRequest();
        var result = await _mediator.Send(new AddPlaceholderCrewmateCommand(body.Name, body.PaymentPlatforms));
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

    [HttpDelete("{userId:int}/block")]
    public async Task<IActionResult> Unblock(int userId)
    {
        var result = await _mediator.Send(new UnblockCrewmateCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/kick")]
    public async Task<IActionResult> Kick(int userId, [FromBody] KickCrewmateRequest body)
    {
        body ??= new KickCrewmateRequest();
        var result = await _mediator.Send(new KickCrewmateCommand(userId, body.Reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/nominate-roles")]
    public async Task<IActionResult> NominateRoles(int userId, [FromBody] CrewRoleChangeRequest body)
    {
        body ??= new CrewRoleChangeRequest();
        var result = await _mediator.Send(new NominateCrewRolesCommand(userId, body.Roles));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/demote-roles")]
    public async Task<IActionResult> DemoteRoles(int userId, [FromBody] CrewRoleChangeRequest body)
    {
        body ??= new CrewRoleChangeRequest();
        var result = await _mediator.Send(new DemoteCrewRolesCommand(userId, body.Roles));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{userId:int}/can-attach-files")]
    public async Task<IActionResult> ToggleCanAttachFiles(int userId, [FromBody] ToggleCanAttachFilesRequest body)
    {
        body ??= new ToggleCanAttachFilesRequest();
        var result = await _mediator.Send(new ToggleCanAttachFilesCommand(userId, body.CanAttachFiles));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/claim-identity")]
    public async Task<IActionResult> ClaimIdentity(int userId)
    {
        var result = await _mediator.Send(new ClaimPlaceholderIdentityCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{userId:int}/allow-rejoin")]
    public async Task<IActionResult> AllowRejoin(int userId)
    {
        var result = await _mediator.Send(new AllowCrewmateRejoinCommand(userId));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
