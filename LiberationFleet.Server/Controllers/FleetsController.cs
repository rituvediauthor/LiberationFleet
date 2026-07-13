using LiberationFleet.Server.Application.Features.Crews.Commands.SubmitJoinRequest;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Fleets.Commands.AcceptFleetRules;
using LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleet;
using LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleetRule;
using LiberationFleet.Server.Application.Features.Fleets.Commands.DeleteFleetRule;
using LiberationFleet.Server.Application.Features.Fleets.Commands.InviteCrewToFleet;
using LiberationFleet.Server.Application.Features.Fleets.Commands.ProposeFleetKickCrew;
using LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleetRule;
using LiberationFleet.Server.Application.Features.Fleets.Commands.RecordFleetGifts;
using LiberationFleet.Server.Application.Features.Fleets.Commands.SubmitFleetJoinRequest;
using LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleet;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetCurrentFleet;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetChatRooms;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetCrewDetail;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetCrews;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetEmergencies;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetGiftLog;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetLibraryStatus;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetNextAid;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetRule;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetRules;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetReceptionOrder;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetFleetStatus;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetMyFleetJoinRequests;
using LiberationFleet.Server.Application.Features.Fleets.Queries.GetPublicFleetRules;
using LiberationFleet.Server.Application.Features.Fleets.Queries.LookupCrewByJoinCode;
using LiberationFleet.Server.Application.Features.Fleets.Queries.SearchFleets;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class FleetsController : ControllerBase
{
    private readonly IMediator _mediator;

    public FleetsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("status")]
    public async Task<IActionResult> GetStatus()
    {
        var result = await _mediator.Send(new GetFleetStatusQuery());
        return Ok(result);
    }

    [HttpPost("accept-rules")]
    public async Task<IActionResult> AcceptRules([FromBody] AcceptFleetRulesBody body)
    {
        var result = await _mediator.Send(new AcceptFleetRulesCommand(body.AcceptedRuleIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("lookup-crew")]
    public async Task<IActionResult> LookupCrew([FromQuery] string joinCode)
    {
        var result = await _mediator.Send(new LookupCrewByJoinCodeQuery(joinCode));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("invite-crew")]
    public async Task<IActionResult> InviteCrew([FromBody] InviteCrewToFleetRequest body)
    {
        var result = await _mediator.Send(new InviteCrewToFleetCommand(body.JoinCode));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateFleetRequest body)
    {
        var result = await _mediator.Send(new CreateFleetCommand(
            body.Name,
            body.Privacy,
            body.Scope,
            body.ZipCode,
            body.RadiusMiles));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchFleetsQuery query)
    {
        var result = await _mediator.Send(query);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{fleetId:int}/public-rules")]
    public async Task<IActionResult> GetPublicRules(int fleetId)
    {
        var result = await _mediator.Send(new GetPublicFleetRulesQuery(fleetId, null));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("public-rules")]
    public async Task<IActionResult> GetPublicRulesByJoinCode([FromQuery] string joinCode)
    {
        var result = await _mediator.Send(new GetPublicFleetRulesQuery(null, joinCode));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("join-request")]
    public async Task<IActionResult> SubmitJoinRequest([FromBody] SubmitFleetJoinRequestBody body)
    {
        var result = await _mediator.Send(new SubmitFleetJoinRequestCommand(
            body.FleetId,
            body.JoinCode,
            body.AcceptedRuleIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("join-requests/mine")]
    public async Task<IActionResult> GetMyJoinRequests()
    {
        var result = await _mediator.Send(new GetMyFleetJoinRequestsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrent()
    {
        var result = await _mediator.Send(new GetCurrentFleetQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrent([FromBody] UpdateFleetRequest body)
    {
        var result = await _mediator.Send(new UpdateFleetCommand(
            body.Name,
            body.Privacy,
            body.Scope,
            body.ZipCode,
            body.RadiusMiles,
            body.RequireApprovalForEdits,
            body.LibraryOfThingsEnabled,
            body.AllowCrewmateFileAttachments,
            body.MinimumCrewmateTenureDaysForAttachments,
            body.MinimumContributionForAttachments,
            body.MinimumCrewmateTenureDaysForProposals,
            body.MinimumContributionForProposals));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/crews")]
    public async Task<IActionResult> GetCurrentCrews()
    {
        var result = await _mediator.Send(new GetFleetCrewsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("crews/{crewId:int}")]
    public async Task<IActionResult> GetCrewDetail(int crewId)
    {
        var result = await _mediator.Send(new GetFleetCrewDetailQuery(crewId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("crews/{crewId:int}/kick")]
    public async Task<IActionResult> ProposeKickCrew(int crewId, [FromBody] ProposeFleetKickCrewBody? body)
    {
        var result = await _mediator.Send(new ProposeFleetKickCrewCommand(crewId, body?.Reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("crews/{crewId:int}/join")]
    public async Task<IActionResult> JoinCrew(int crewId, [FromBody] SubmitJoinRequestBody? body)
    {
        var result = await _mediator.Send(new SubmitJoinRequestCommand(
            crewId,
            null,
            body?.AcceptedRuleIds ?? Array.Empty<int>()));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/gift-log")]
    public async Task<IActionResult> GetGiftLog(
        [FromQuery] int limit = 50,
        [FromQuery] DateTime? beforeCreatedAt = null,
        [FromQuery] int? beforeId = null)
    {
        var result = await _mediator.Send(new GetFleetGiftLogQuery(limit, beforeCreatedAt, beforeId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/reception-order")]
    public async Task<IActionResult> GetReceptionOrder([FromQuery] int limit = 30)
    {
        var items = await _mediator.Send(new GetFleetReceptionOrderQuery(limit));
        return Ok(new
        {
            success = true,
            message = "Fleet reception order loaded.",
            items
        });
    }

    [HttpPost("current/gifts")]
    [HttpPost("current/gifts/batch")]
    public async Task<IActionResult> RecordGiftsBatch([FromBody] RecordFleetGiftsCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/next-aid")]
    public async Task<IActionResult> GetNextAid()
    {
        var nextAid = await _mediator.Send(new GetFleetNextAidQuery());
        return Ok(new
        {
            success = true,
            message = nextAid is null ? "No aid needed right now." : "Next aid loaded.",
            nextAid
        });
    }

    [HttpGet("current/emergencies")]
    public async Task<IActionResult> GetEmergencies()
    {
        var result = await _mediator.Send(new GetFleetEmergenciesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/chats")]
    public async Task<IActionResult> GetChats()
    {
        var result = await _mediator.Send(new GetFleetChatRoomsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/rules")]
    public async Task<IActionResult> GetRules()
    {
        var result = await _mediator.Send(new GetFleetRulesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/rules/{id:int}")]
    public async Task<IActionResult> GetRule(int id)
    {
        var result = await _mediator.Send(new GetFleetRuleQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("current/rules")]
    public async Task<IActionResult> CreateRule([FromBody] WriteFleetRuleRequest body)
    {
        var result = await _mediator.Send(new CreateFleetRuleCommand(body.IsPublic, body.Title, body.Description));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("current/rules/{id:int}")]
    public async Task<IActionResult> UpdateRule(int id, [FromBody] WriteFleetRuleRequest body)
    {
        var result = await _mediator.Send(new UpdateFleetRuleCommand(id, body.IsPublic, body.Title, body.Description));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("current/rules/{id:int}")]
    public async Task<IActionResult> DeleteRule(int id)
    {
        var result = await _mediator.Send(new DeleteFleetRuleCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("current/library-status")]
    public async Task<IActionResult> GetLibraryStatus()
    {
        var result = await _mediator.Send(new GetFleetLibraryStatusQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
