using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Commands.CreateGiftComment;

public record CreateGiftCommentCommand(
    int GiftId,
    int? ParentCommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds,
    string? Preview = null) : IRequest<GiftEngagementOperationResponse>;

public class CreateGiftCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IGiftRepository giftRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateGiftCommentCommand, GiftEngagementOperationResponse>
{
    public async Task<GiftEngagementOperationResponse> Handle(
        CreateGiftCommentCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new GiftEngagementOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new GiftEngagementOperationResponse { Success = false, Message = "Encrypted comment content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new GiftEngagementOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var gift = await giftRepository.GetByIdWithUsersAsync(request.GiftId, cancellationToken);
        if (gift is null || gift.CrewId != membership.CrewId)
        {
            return new GiftEngagementOperationResponse { Success = false, Message = "Gift not found." };
        }

        GiftComment? parentComment = null;
        int? threadRootId = null;
        int? replyToCommentId = null;
        if (request.ParentCommentId.HasValue)
        {
            parentComment = await giftRepository.GetCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment is null || parentComment.GiftId != gift.Id)
            {
                return new GiftEngagementOperationResponse { Success = false, Message = "Parent comment not found." };
            }

            (threadRootId, replyToCommentId) = CommentThread.ResolveNewReply(
                parentComment.Id,
                parentComment.ParentCommentId);
        }

        var utcNow = DateTime.UtcNow;
        var comment = new GiftComment
        {
            GiftId = gift.Id,
            AuthorUserId = userId,
            ParentCommentId = threadRootId,
            ReplyToCommentId = replyToCommentId,
            CreatedAt = utcNow
        };

        await giftRepository.AddCommentAsync(comment, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.GiftComment,
            ResourceId = comment.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var actionUrl = $"/app/crew/gift-log/{gift.Id}?commentId={comment.Id}";
        if (parentComment is not null && parentComment.AuthorUserId != userId)
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = parentComment.AuthorUserId,
                CrewId = membership.CrewId,
                Kind = NotificationKind.NewGiftReply,
                Title = "New reply",
                Body = NotificationPreview.BodyOrFallback(request.Preview, "Someone replied to your gift log comment."),
                ActionUrl = actionUrl,
                RelatedEntityId = gift.Id,
                SecondaryEntityId = comment.Id,
                ActorUserId = userId
            }, cancellationToken);
        }
        else if (parentComment is null)
        {
            var notifyUserIds = new List<int>();
            if (gift.GiverUserId != userId)
            {
                notifyUserIds.Add(gift.GiverUserId);
            }

            if (gift.RecipientUserId != userId && !notifyUserIds.Contains(gift.RecipientUserId))
            {
                notifyUserIds.Add(gift.RecipientUserId);
            }

            if (notifyUserIds.Count > 0)
            {
                await notificationService.NotifyUsersAsync(
                    notifyUserIds.Select(targetUserId => new CreateNotificationRequest
                    {
                        UserId = targetUserId,
                        CrewId = membership.CrewId,
                        Kind = NotificationKind.NewGiftComment,
                        Title = "New gift comment",
                        Body = NotificationPreview.BodyOrFallback(request.Preview, "A new comment was posted on a gift log entry."),
                        ActionUrl = actionUrl,
                        RelatedEntityId = gift.Id,
                        SecondaryEntityId = comment.Id,
                        ActorUserId = userId
                    }),
                    cancellationToken);
            }
        }

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.GiftComment,
            ResourceId = comment.Id,
            ParentResourceId = gift.Id,
            ActionUrl = actionUrl,
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            Preview = request.Preview
        }, cancellationToken);

        return new GiftEngagementOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id
        };
    }
}
