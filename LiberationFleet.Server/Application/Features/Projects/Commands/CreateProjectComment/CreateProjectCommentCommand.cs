using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Projects.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Projects.Commands.CreateProjectComment;

public record CreateProjectCommentCommand(
    int PostId,
    int? ParentCommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<ProjectOperationResponse>;

public class CreateProjectCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProjectRepository projectRepository,
    ICryptoRepository cryptoRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProjectCommentCommand, ProjectOperationResponse>
{
    public async Task<ProjectOperationResponse> Handle(CreateProjectCommentCommand request, CancellationToken cancellationToken)
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

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ProjectOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        if (request.ParentCommentId.HasValue)
        {
            var parent = await projectRepository.GetCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parent is null || parent.ProjectPostId != post.Id)
            {
                return new ProjectOperationResponse { Success = false, Message = "Parent comment not found." };
            }
        }

        var utcNow = DateTime.UtcNow;
        var comment = new ProjectComment
        {
            ProjectPostId = post.Id,
            AuthorUserId = userId,
            ParentCommentId = request.ParentCommentId,
            CreatedAt = utcNow
        };

        await projectRepository.AddCommentAsync(comment, cancellationToken);
        post.LastActivityAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProjectComment,
            ResourceId = comment.Id.ToString(),
            CrewId = post.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProjectOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id
        };
    }
}
