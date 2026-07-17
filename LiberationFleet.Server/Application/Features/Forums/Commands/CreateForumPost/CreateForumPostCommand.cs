using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Commands.CreateForumPost;

public record CreateForumPostCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    bool IsAdultContent,
    IReadOnlyList<int> MentionedUserIds,
    string? Preview = null) : IRequest<ForumOperationResponse>;

public class CreateForumPostCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateForumPostCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(CreateForumPostCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ForumOperationResponse { Success = false, Message = "Encrypted post content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var post = new ForumPost
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow,
            IsAdultContent = request.IsAdultContent
        };

        await forumRepository.AddPostAsync(post, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ForumPost,
            ResourceId = post.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewIfNotMutedAsync(
            membership.CrewId,
            NotificationKind.NewForumPost,
            MutedContentType.Forum,
            post.Id,
            "New forum post",
            NotificationPreview.BodyOrFallback(request.Preview, "A new forum post was published."),
            $"/app/crew/forums/{post.Id}",
            relatedEntityId: post.Id,
            excludeUserId: userId,
            cancellationToken: cancellationToken);

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ForumPost,
            ResourceId = post.Id,
            ActionUrl = $"/app/crew/forums/{post.Id}",
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            Preview = request.Preview
        }, cancellationToken);

        return new ForumOperationResponse
        {
            Success = true,
            Message = "Forum post created.",
            PostId = post.Id
        };
    }
}
