using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Commands.UpdateProjectComment;

public record UpdateProjectCommentCommand(
    int PostId,
    int CommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<ProjectOperationResponse>;

public class UpdateProjectCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProjectCommentCommand, ProjectOperationResponse>
{
    public async Task<ProjectOperationResponse> Handle(UpdateProjectCommentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProjectOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProjectOperationResponse { Success = false, Message = "Encrypted comment content is required." };
        }

        var userId = currentUser.UserId.Value;
        var post = await projectRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ProjectOperationResponse { Success = false, Message = "Project post not found." };
        }

        var comment = await projectRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ProjectPostId != post.Id)
        {
            return new ProjectOperationResponse { Success = false, Message = "Comment not found." };
        }

        if (comment.AuthorUserId != userId)
        {
            return new ProjectOperationResponse { Success = false, Message = "Only the author can edit this comment." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ProjectOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        post.LastActivityAt = DateTime.UtcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProjectComment,
            ResourceId = comment.Id.ToString(),
            CrewId = post.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProjectOperationResponse { Success = true, Message = "Comment updated.", CommentId = comment.Id };
    }
}
