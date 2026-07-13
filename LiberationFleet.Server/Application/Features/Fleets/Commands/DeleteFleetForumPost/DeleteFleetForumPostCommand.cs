using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.DeleteFleetForumPost;

public record DeleteFleetForumPostCommand(int PostId) : IRequest<ForumOperationResponse>;

public class DeleteFleetForumPostCommandHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteFleetForumPostCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(DeleteFleetForumPostCommand request, CancellationToken cancellationToken)
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

        if (!post.FleetId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Not a fleet forum post." };
        }

        if (post.AuthorUserId != userId)
        {
            return new ForumOperationResponse { Success = false, Message = "Only the author can delete this post." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        post.IsDeleted = true;
        post.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ForumOperationResponse { Success = true, Message = "Forum post deleted." };
    }
}
