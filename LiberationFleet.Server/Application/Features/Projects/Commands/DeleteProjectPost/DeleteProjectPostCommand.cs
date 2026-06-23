using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Commands.DeleteProjectPost;

public record DeleteProjectPostCommand(int PostId) : IRequest<ProjectOperationResponse>;

public class DeleteProjectPostCommandHandler(
    ICurrentUserService currentUser,
    IProjectRepository projectRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteProjectPostCommand, ProjectOperationResponse>
{
    public async Task<ProjectOperationResponse> Handle(DeleteProjectPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProjectOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var post = await projectRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ProjectOperationResponse { Success = false, Message = "Project post not found." };
        }

        if (post.AuthorUserId != userId)
        {
            return new ProjectOperationResponse { Success = false, Message = "Only the author can delete this post." };
        }

        post.IsDeleted = true;
        post.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProjectOperationResponse { Success = true, Message = "Project post deleted." };
    }
}
