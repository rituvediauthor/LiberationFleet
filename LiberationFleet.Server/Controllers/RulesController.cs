using LiberationFleet.Server.Application.Features.Rules.Commands.CreateCrewRule;
using LiberationFleet.Server.Application.Features.Rules.Commands.DeleteCrewRule;
using LiberationFleet.Server.Application.Features.Rules.Commands.UpdateCrewRule;
using LiberationFleet.Server.Application.Features.Rules.Contracts;
using LiberationFleet.Server.Application.Features.Rules.Queries.GetCrewRule;
using LiberationFleet.Server.Application.Features.Rules.Queries.GetCrewRules;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/rules")]
[Authorize]
public class RulesController : ControllerBase
{
    private readonly IMediator _mediator;

    public RulesController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _mediator.Send(new GetCrewRulesQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _mediator.Send(new GetCrewRuleQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateRuleRequest body)
    {
        var result = await _mediator.Send(new CreateCrewRuleCommand(
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.PlaintextTitle,
            body.PlaintextDescription));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateRuleRequest body)
    {
        var result = await _mediator.Send(new UpdateCrewRuleCommand(
            id,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.PlaintextTitle,
            body.PlaintextDescription,
            body.PlaintextOldTitle,
            body.PlaintextOldDescription));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id, [FromBody] DeleteRuleRequest? body)
    {
        body ??= new DeleteRuleRequest();
        var result = await _mediator.Send(new DeleteCrewRuleCommand(
            id,
            body.PlaintextTitle,
            body.PlaintextDescription));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
