using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleetForumComment;

public record UpdateFleetForumCommentCommand(
    int PostId,
    int CommentId,
    string Body) : IRequest<ForumOperationResponse>;

public class UpdateFleetForumCommentCommandHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateFleetForumCommentCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(UpdateFleetForumCommentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Body))
        {
            return new ForumOperationResponse { Success = false, Message = "Comment body is required." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ForumOperationResponse { Success = false, Message = "Forum post not found." };
        }

        if (!post.FleetId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Not a fleet forum post." };
        }

        var comment = await forumRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ForumPostId != post.Id)
        {
            return new ForumOperationResponse { Success = false, Message = "Comment not found." };
        }

        if (comment.AuthorUserId != userId)
        {
            return new ForumOperationResponse { Success = false, Message = "Only the author can edit this comment." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        comment.Body = request.Body.Trim();
        post.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ForumOperationResponse { Success = true, Message = "Comment updated.", CommentId = comment.Id };
    }
}
