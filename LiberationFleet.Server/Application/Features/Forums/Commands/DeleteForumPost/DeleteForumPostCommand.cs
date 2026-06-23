using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Commands.DeleteForumPost;

public record DeleteForumPostCommand(int PostId) : IRequest<ForumOperationResponse>;

public class DeleteForumPostCommandHandler(
    ICurrentUserService currentUser,
    IForumRepository forumRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteForumPostCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(DeleteForumPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ForumOperationResponse { Success = false, Message = "Forum post not found." };
        }

        if (post.AuthorUserId != userId)
        {
            return new ForumOperationResponse { Success = false, Message = "Only the author can delete this post." };
        }

        post.IsDeleted = true;
        post.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ForumOperationResponse { Success = true, Message = "Forum post deleted." };
    }
}
