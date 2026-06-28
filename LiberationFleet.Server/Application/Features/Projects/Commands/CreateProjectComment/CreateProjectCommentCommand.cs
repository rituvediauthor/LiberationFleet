using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
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
    NotificationService notificationService,
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

        ProjectComment? parentComment = null;
        int? threadRootId = null;
        int? replyToCommentId = null;
        if (request.ParentCommentId.HasValue)
        {
            parentComment = await projectRepository.GetCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment is null || parentComment.ProjectPostId != post.Id)
            {
                return new ProjectOperationResponse { Success = false, Message = "Parent comment not found." };
            }

            (threadRootId, replyToCommentId) = CommentThread.ResolveNewReply(
                parentComment.Id,
                parentComment.ParentCommentId);
        }

        var utcNow = DateTime.UtcNow;
        var comment = new ProjectComment
        {
            ProjectPostId = post.Id,
            AuthorUserId = userId,
            ParentCommentId = threadRootId,
            ReplyToCommentId = replyToCommentId,
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

        var actionUrl = $"/app/crew/projects/{post.Id}?commentId={comment.Id}";
        if (parentComment is not null && parentComment.AuthorUserId != userId)
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = parentComment.AuthorUserId,
                CrewId = post.CrewId,
                Kind = NotificationKind.NewReply,
                Title = "New reply",
                Body = "Someone replied to your project comment.",
                ActionUrl = actionUrl,
                RelatedEntityId = post.Id,
                SecondaryEntityId = comment.Id
            }, cancellationToken);
        }
        else if (parentComment is null)
        {
            await notificationService.NotifyCrewIfNotMutedAsync(
                post.CrewId,
                NotificationKind.NewProjectComment,
                MutedContentType.Project,
                post.Id,
                "New project comment",
                "A new comment was posted on a project thread.",
                actionUrl,
                relatedEntityId: post.Id,
                secondaryEntityId: comment.Id,
                excludeUserId: userId,
                cancellationToken: cancellationToken);
        }

        return new ProjectOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id
        };
    }
}
