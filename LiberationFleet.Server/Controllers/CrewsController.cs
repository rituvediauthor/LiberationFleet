using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Application.Features.Crews.Commands.LeaveCrew;
using LiberationFleet.Server.Application.Features.Crews.Commands.SubmitJoinRequest;
using LiberationFleet.Server.Application.Features.Crews.Commands.UpdateCrew;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetCrewPaymentPlatforms;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrew;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetMyJoinRequests;
using LiberationFleet.Server.Application.Features.Crews.Queries.GetPublicCrewRules;
using LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/[controller]")]
[Authorize]
public class CrewsController : ControllerBase
{
    private readonly IMediator _mediator;

    public CrewsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet("membership")]
    public async Task<IActionResult> GetMembership()
    {
        var result = await _mediator.Send(new GetMyCrewMembershipQuery());
        return Ok(result);
    }

    [HttpGet("current")]
    public async Task<IActionResult> GetCurrentCrew()
    {
        var result = await _mediator.Send(new GetMyCrewQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("current")]
    public async Task<IActionResult> UpdateCurrentCrew([FromBody] UpdateCrewRequest body)
    {
        var result = await _mediator.Send(new UpdateCrewCommand(
            body.Name,
            body.MaxSize,
            body.Privacy,
            body.Scope,
            body.ZipCode,
            body.RadiusMiles,
            body.AllowSurvivalThresholds,
            body.RequireApprovalForEdits,
            body.InNeedDefaultThreshold,
            body.LibraryOfThingsEnabled,
            body.MemberCycleCapMode,
            body.MemberCycleCapFixedAmount,
            body.MemberCycleCapMultiplier,
            body.NonMemberCycleCapMode,
            body.NonMemberCycleCapFixedAmount,
            body.NonMemberCycleCapMultiplier,
            body.AllowCrewmateFileAttachments,
            body.MinimumCrewmateTenureDaysForAttachments,
            body.MinimumContributionForAttachments,
            body.MinimumCrewmateTenureDaysForProposals,
            body.MinimumContributionForProposals));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("leave")]
    public async Task<IActionResult> LeaveCrew()
    {
        var result = await _mediator.Send(new LeaveCrewCommand());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateCrewCommand command)
    {
        var result = await _mediator.Send(command);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("payment-platforms")]
    public async Task<IActionResult> GetPaymentPlatforms([FromQuery] bool otherCrewmatesOnly = false)
    {
        var result = await _mediator.Send(new GetCrewPaymentPlatformsQuery(otherCrewmatesOnly));
        return Ok(result);
    }

    [HttpPost("search")]
    public async Task<IActionResult> Search([FromBody] SearchCrewsQuery query)
    {
        var result = await _mediator.Send(query);
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{crewId:int}/public-rules")]
    public async Task<IActionResult> GetPublicRules(int crewId)
    {
        var result = await _mediator.Send(new GetPublicCrewRulesQuery(crewId, null));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("public-rules")]
    public async Task<IActionResult> GetPublicRulesByJoinCode([FromQuery] string joinCode)
    {
        var result = await _mediator.Send(new GetPublicCrewRulesQuery(null, joinCode));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("join-request")]
    public async Task<IActionResult> SubmitJoinRequest([FromBody] SubmitJoinRequestBody body)
    {
        var result = await _mediator.Send(new SubmitJoinRequestCommand(
            body.CrewId,
            body.JoinCode,
            body.AcceptedRuleIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("join-requests/mine")]
    public async Task<IActionResult> GetMyJoinRequests()
    {
        var result = await _mediator.Send(new GetMyJoinRequestsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
