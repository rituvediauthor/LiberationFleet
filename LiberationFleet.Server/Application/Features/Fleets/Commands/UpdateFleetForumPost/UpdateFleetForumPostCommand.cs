using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.UpdateFleetForumPost;

public record UpdateFleetForumPostCommand(
    int PostId,
    string Title,
    string Body) : IRequest<ForumOperationResponse>;

public class UpdateFleetForumPostCommandHandler(
    ICurrentUserService currentUser,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateFleetForumPostCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(UpdateFleetForumPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Title) || string.IsNullOrWhiteSpace(request.Body))
        {
            return new ForumOperationResponse { Success = false, Message = "Title and body are required." };
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
            return new ForumOperationResponse { Success = false, Message = "Only the author can edit this post." };
        }

        if (!await fleetRepository.IsUserInFleetAsync(userId, post.FleetId.Value, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        post.Title = request.Title.Trim();
        post.Body = request.Body.Trim();
        post.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ForumOperationResponse { Success = true, Message = "Forum post updated." };
    }
}
