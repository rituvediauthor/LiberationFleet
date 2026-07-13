using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Forums.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Forums.Commands.UpdateForumComment;

public record UpdateForumCommentCommand(
    int PostId,
    int CommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds) : IRequest<ForumOperationResponse>;

public class UpdateForumCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IForumRepository forumRepository,
    ICryptoRepository cryptoRepository,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateForumCommentCommand, ForumOperationResponse>
{
    public async Task<ForumOperationResponse> Handle(UpdateForumCommentCommand request, CancellationToken cancellationToken)
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

        if (!post.CrewId.HasValue)
        {
            return new ForumOperationResponse { Success = false, Message = "Not a crew forum post." };
        }

        var crewId = post.CrewId.Value;
        var comment = await forumRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ForumPostId != post.Id)
        {
            return new ForumOperationResponse { Success = false, Message = "Comment not found." };
        }

        if (comment.AuthorUserId != userId)
        {
            return new ForumOperationResponse { Success = false, Message = "Only the author can edit this comment." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, crewId, cancellationToken))
        {
            return new ForumOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        post.LastActivityAt = DateTime.UtcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ForumComment,
            ResourceId = comment.Id.ToString(),
            CrewId = crewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = crewId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ForumComment,
            ResourceId = comment.Id,
            ParentResourceId = post.Id,
            ActionUrl = $"/app/crew/forums/{post.Id}?commentId={comment.Id}",
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            IsUpdate = true
        }, cancellationToken);

        return new ForumOperationResponse { Success = true, Message = "Comment updated.", CommentId = comment.Id };
    }
}
