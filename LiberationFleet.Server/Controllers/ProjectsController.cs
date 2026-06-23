using LiberationFleet.Server.Application.Features.Projects.Commands.CreateProjectComment;
using LiberationFleet.Server.Application.Features.Projects.Commands.CreateProjectPost;
using LiberationFleet.Server.Application.Features.Projects.Commands.DeleteProjectPost;
using LiberationFleet.Server.Application.Features.Projects.Commands.UpdateProjectPost;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Application.Features.Projects.Queries.GetCrewProjectPosts;
using LiberationFleet.Server.Application.Features.Projects.Queries.GetProjectCommentReplies;
using LiberationFleet.Server.Application.Features.Projects.Queries.GetProjectPostDetail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/projects")]
[Authorize]
public class ProjectsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ProjectsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _mediator.Send(new GetCrewProjectPostsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _mediator.Send(new GetProjectPostDetailQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{postId:int}/comments/{parentCommentId:int}/replies")]
    public async Task<IActionResult> GetCommentReplies(int postId, int parentCommentId)
    {
        var result = await _mediator.Send(new GetProjectCommentRepliesQuery(postId, parentCommentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateProjectPostRequest body)
    {
        var result = await _mediator.Send(new CreateProjectPostCommand(body.Nonce, body.Ciphertext, body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateProjectPostRequest body)
    {
        var result = await _mediator.Send(new UpdateProjectPostCommand(id, body.Nonce, body.Ciphertext, body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _mediator.Send(new DeleteProjectPostCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> CreateComment(int id, [FromBody] CreateProjectCommentRequest body)
    {
        var result = await _mediator.Send(new CreateProjectCommentCommand(
            id,
            body.ParentCommentId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
