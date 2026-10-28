using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Commands.CreateFleetForumComment;

public record CreateFleetForumCommentCommand(
    int PostId,
    int? ParentCommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds,
    string? Preview = null) : IRequest<ForumOperationResponse>;

public class CreateFleetForumCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IFleetRepository fleetRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateFleetForumCommentCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(CreateFleetForumCommentCommand request, CancellationToken cancellationToken)
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
            return new ForumOperationResponse { Success = false, Message = "Post not found." };
        }

        if (!post.FleetId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Not a fleet post." };
        }

        var fleetId = post.FleetId.Value;
        if (!await fleetRepository.IsUserInFleetAsync(userId, fleetId, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this fleet." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in a crew." };
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
            FleetId = fleetId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var actionUrl = $"/app/fleet/forums/{post.Id}?commentId={comment.Id}";
        var actor = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        var actorName = UserDisplay.Name(actor);
        var mentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds);
        var forumLocation = NotificationCopy.ForumPostLabel(post.Title, fleet: true);
        if (parentComment is not null && parentComment.AuthorUserId != userId)
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = parentComment.AuthorUserId,
                CrewId = membership.CrewId,
                Kind = NotificationKind.NewFleetReply,
                Title = NotificationCopy.RepliedOnFleetForum(actorName, toComment: true),
                Body = NotificationCopy.PreviewOr(request.Preview, $"{actorName} replied to your fleet comment."),
                ActionUrl = actionUrl,
                RelatedEntityId = post.Id,
                SecondaryEntityId = comment.Id,
                ActorUserId = userId,
                MuteForumPostId = post.Id,
                MuteFleetSpaceId = fleetId,
                RecipientWasMentioned = mentionedUserIds.Contains(parentComment.AuthorUserId)
            }, cancellationToken);
        }

        if (post.AuthorUserId != userId
            && (parentComment is null || parentComment.AuthorUserId != post.AuthorUserId))
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = post.AuthorUserId,
                CrewId = membership.CrewId,
                Kind = NotificationKind.NewFleetForumComment,
                Title = NotificationCopy.RepliedOnFleetForum(actorName, toComment: false),
                Body = NotificationCopy.PreviewOr(request.Preview, $"{actorName} commented on your fleet post."),
                ActionUrl = actionUrl,
                RelatedEntityId = post.Id,
                SecondaryEntityId = comment.Id,
                ActorUserId = userId,
                MuteForumPostId = post.Id,
                MuteFleetSpaceId = fleetId,
                RecipientWasMentioned = mentionedUserIds.Contains(post.AuthorUserId)
            }, cancellationToken);
        }

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = membership.CrewId,
            FleetId = fleetId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ForumComment,
            ResourceId = comment.Id,
            ParentResourceId = post.Id,
            ActionUrl = actionUrl,
            MentionedUserIds = mentionedUserIds,
            Preview = request.Preview,
            LocationLabel = forumLocation
        }, cancellationToken);

        return new ForumOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id
        };
    }
}
