using LiberationFleet.Server.Application.Features.Forums.Commands.CreateForumComment;
using LiberationFleet.Server.Application.Features.Forums.Commands.CreateForumPost;
using LiberationFleet.Server.Application.Features.Forums.Commands.DeleteForumPost;
using LiberationFleet.Server.Application.Features.Forums.Commands.UpdateForumComment;
using LiberationFleet.Server.Application.Features.Forums.Commands.UpdateForumPost;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Forums.Queries.GetCrewForumPosts;
using LiberationFleet.Server.Application.Features.Forums.Queries.GetForumCommentReplies;
using LiberationFleet.Server.Application.Features.Forums.Queries.GetForumPostDetail;
using MediatR;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace LiberationFleet.Server.Controllers;

[ApiController]
[Route("api/forums")]
[Authorize]
public class ForumsController : ControllerBase
{
    private readonly IMediator _mediator;

    public ForumsController(IMediator mediator)
    {
        _mediator = mediator;
    }

    [HttpGet]
    public async Task<IActionResult> GetList()
    {
        var result = await _mediator.Send(new GetCrewForumPostsQuery());
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{id:int}")]
    public async Task<IActionResult> GetDetail(int id)
    {
        var result = await _mediator.Send(new GetForumPostDetailQuery(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpGet("{postId:int}/comments/{parentCommentId:int}/replies")]
    public async Task<IActionResult> GetCommentReplies(int postId, int parentCommentId)
    {
        var result = await _mediator.Send(new GetForumCommentRepliesQuery(postId, parentCommentId));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost]
    public async Task<IActionResult> Create([FromBody] CreateForumPostRequest body)
    {
        var result = await _mediator.Send(new CreateForumPostCommand(body.Nonce, body.Ciphertext, body.KeyVersion, body.IsAdultContent));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{id:int}")]
    public async Task<IActionResult> Update(int id, [FromBody] UpdateForumPostRequest body)
    {
        var result = await _mediator.Send(new UpdateForumPostCommand(id, body.Nonce, body.Ciphertext, body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpDelete("{id:int}")]
    public async Task<IActionResult> Delete(int id)
    {
        var result = await _mediator.Send(new DeleteForumPostCommand(id));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPost("{id:int}/comments")]
    public async Task<IActionResult> CreateComment(int id, [FromBody] CreateForumCommentRequest body)
    {
        var result = await _mediator.Send(new CreateForumCommentCommand(
            id,
            body.ParentCommentId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }

    [HttpPut("{postId:int}/comments/{commentId:int}")]
    public async Task<IActionResult> UpdateComment(int postId, int commentId, [FromBody] UpdateForumCommentRequest body)
    {
        body ??= new UpdateForumCommentRequest();
        var result = await _mediator.Send(new UpdateForumCommentCommand(
            postId,
            commentId,
            body.Nonce,
            body.Ciphertext,
            body.KeyVersion));
        return result.Success ? Ok(result) : BadRequest(result);
    }
}
