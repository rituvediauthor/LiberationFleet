using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposal;
using LiberationFleet.Server.Application.Features.Proposals.Commands.CreateFleetProposal;
using LiberationFleet.Server.Application.Features.Proposals.Commands.CreateKickFromComment;
using LiberationFleet.Server.Application.Features.Proposals.Commands.CreateKickFromProposalAuthor;
using LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposalComment;
using LiberationFleet.Server.Application.Features.Proposals.Commands.UpdateProposalComment;
using LiberationFleet.Server.Application.Features.Proposals.Commands.DeleteProposal;
using LiberationFleet.Server.Application.Features.Proposals.Commands.RerollProposalAlias;
using LiberationFleet.Server.Application.Features.Proposals.Commands.UpdateProposal;
using LiberationFleet.Server.Application.Features.Proposals.Commands.VoteProposal;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Queries.GetCrewProposals;
using LiberationFleet.Server.Application.Features.Proposals.Queries.GetFleetProposals;
using LiberationFleet.Server.Application.Features.Proposals.Queries.GetProposalDetail;
using LiberationFleet.Server.Application.Features.Proposals.Queries.GetProposalCommentReplies;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/proposals")]
[Authorize]
public class ProposalsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProposalsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList([FromQuery] string status = "Pending", [FromQuery] string? scope = null)
    {
        if (string.Equals(scope, "fleet", StringComparison.OrdinalIgnoreCase))
        {
            var fleetResult = await _mediator.Send(new GetFleetProposalsQuery(status));
            return fleetResult.Success ? Ok(fleetResult) : BadRequest(fleetResult);
        }

        var result = await _mediator.Send(new GetCrewProposalsQuery(status));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _mediator.Send(new GetProposalDetailQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{proposalId:int}/comments/{parentCommentId:int}/replies")]
    public async Task<IActionResult> GetCommentReplies(int proposalId, int parentCommentId)
    {
        var result = await _mediator.Send(new GetProposalCommentRepliesQuery(proposalId, parentCommentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProposalRequest body)
    {
        if (string.Equals(body.Scope, "fleet", StringComparison.OrdinalIgnoreCase))
        {
            var fleetResult = await _mediator.Send(new CreateFleetProposalCommand(body.Title ?? string.Empty, body.Description ?? string.Empty));
            return fleetResult.Success ? Ok(fleetResult) : BadRequest(fleetResult);
        }

        var result = await _mediator.Send(new CreateProposalCommand(body.Nonce, body.Ciphertext, body.KeyVersion, body.MentionedUserIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProposalRequest body)
    {
        var result = await _mediator.Send(new UpdateProposalCommand(id, body.Nonce, body.Ciphertext, body.KeyVersion, body.MentionedUserIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _mediator.Send(new DeleteProposalCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/vote")]
    public async Task<IActionResult> Vote(int id, [FromBody] VoteProposalRequest body)
    {
        var result = await _mediator.Send(new VoteProposalCommand(id, body.Vote));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> CreateComment(int id, [FromBody] CreateProposalCommentRequest body)
    {
        var result = await _mediator.Send(new CreateProposalCommentCommand(
            id,
            body.ParentCommentId,
            body.Body,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.MentionedUserIds));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{proposalId:int}/comments/{commentId:int}")]
    public async Task<IActionResult> UpdateComment(int proposalId, int commentId, [FromBody] UpdateProposalCommentRequest body)
    {
        body ??= new UpdateProposalCommentRequest();
        var result = await _mediator.Send(new UpdateProposalCommentCommand(
            proposalId,
            commentId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion,
            body.MentionedUserIds,
            body.Body));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{proposalId:int}/alias/reroll")]
    public async Task<IActionResult> RerollAlias(int proposalId)
    {
        var result = await _mediator.Send(new RerollProposalAliasCommand(proposalId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{proposalId:int}/comments/{commentId:int}/kick")]
    public async Task<IActionResult> KickFromComment(int proposalId, int commentId, [FromBody] KickProposalRequest body)
    {
        body ??= new KickProposalRequest();
        var result = await _mediator.Send(new CreateKickFromCommentCommand(proposalId, commentId, body.Reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{proposalId:int}/author/kick")]
    public async Task<IActionResult> KickFromProposalAuthor(int proposalId, [FromBody] KickProposalRequest body)
    {
        body ??= new KickProposalRequest();
        var result = await _mediator.Send(new CreateKickFromProposalAuthorCommand(proposalId, body.Reason));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
