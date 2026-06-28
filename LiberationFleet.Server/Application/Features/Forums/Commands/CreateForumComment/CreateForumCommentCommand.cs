using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Commands.CreateForumComment;

public record CreateForumCommentCommand(
    int PostId,
    int? ParentCommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<ForumOperationResponse>;

public class CreateForumCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateForumCommentCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(CreateForumCommentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ForumOperationResponse { Success = false, Message = "Encrypted comment content is required." };
        }

        var userId = currentUser.UserId.Value;
        var post = await forumRepository.GetByIdAsync(request.PostId, cancellationToken);
        if (post is null)
        {
            return new ForumOperationResponse { Success = false, Message = "Forum post not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, post.CrewId, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        ForumComment? parentComment = null;
        int? threadRootId = null;
        int? replyToCommentId = null;
        if (request.ParentCommentId.HasValue)
        {
            parentComment = await forumRepository.GetCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment is null || parentComment.ForumPostId != post.Id)
            {
                return new ForumOperationResponse { Success = false, Message = "Parent comment not found." };
            }

            (threadRootId, replyToCommentId) = CommentThread.ResolveNewReply(
                parentComment.Id,
                parentComment.ParentCommentId);
        }

        var utcNow = DateTime.UtcNow;
        var comment = new ForumComment
        {
            ForumPostId = post.Id,
            AuthorUserId = userId,
            ParentCommentId = threadRootId,
            ReplyToCommentId = replyToCommentId,
            CreatedAt = utcNow
        };

        await forumRepository.AddCommentAsync(comment, cancellationToken);
        post.LastActivityAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ForumComment,
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

        var actionUrl = $"/app/crew/forums/{post.Id}?commentId={comment.Id}";
        if (parentComment is not null && parentComment.AuthorUserId != userId)
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = parentComment.AuthorUserId,
                CrewId = post.CrewId,
                Kind = NotificationKind.NewReply,
                Title = "New reply",
                Body = "Someone replied to your forum comment.",
                ActionUrl = actionUrl,
                RelatedEntityId = post.Id,
                SecondaryEntityId = comment.Id
            }, cancellationToken);
        }
        else if (parentComment is null)
        {
            await notificationService.NotifyCrewIfNotMutedAsync(
                post.CrewId,
                NotificationKind.NewForumComment,
                MutedContentType.Forum,
                post.Id,
                "New forum comment",
                "A new comment was posted on a forum thread.",
                actionUrl,
                relatedEntityId: post.Id,
                secondaryEntityId: comment.Id,
                excludeUserId: userId,
                cancellationToken: cancellationToken);
        }

        return new ForumOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id
        };
    }
}
