using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposalComment;

public record CreateProposalCommentCommand(
    int ProposalId,
    int? ParentCommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds) : IRequest<ProposalOperationResponse>;

public class CreateProposalCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    ProposalAnonymousAliasService aliasService,
    NotificationService notificationService,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProposalCommentCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(CreateProposalCommentCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProposalOperationResponse { Success = false, Message = "Encrypted comment content is required." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId!.Value, cancellationToken))
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        ProposalComment? parentComment = null;
        int? threadRootId = null;
        int? replyToCommentId = null;
        if (request.ParentCommentId.HasValue)
        {
            parentComment = await proposalRepository.GetCommentByIdAsync(request.ParentCommentId.Value, cancellationToken);
            if (parentComment is null || parentComment.ProposalId != proposal.Id)
            {
                return new ProposalOperationResponse { Success = false, Message = "Parent comment not found." };
            }

            (threadRootId, replyToCommentId) = CommentThread.ResolveNewReply(
                parentComment.Id,
                parentComment.ParentCommentId);
        }

        var utcNow = DateTime.UtcNow;
        var comment = new ProposalComment
        {
            ProposalId = proposal.Id,
            AuthorUserId = userId,
            ParentCommentId = threadRootId,
            ReplyToCommentId = replyToCommentId,
            CreatedAt = utcNow
        };

        await proposalRepository.AddCommentAsync(comment, cancellationToken);
        proposal.LastActivityAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProposalComment,
            ResourceId = comment.Id.ToString(),
            CrewId = proposal.CrewId!.Value,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        var actionUrl = $"/app/crew/proposals/{proposal.Id}?commentId={comment.Id}";
        var notifyUserId = parentComment?.AuthorUserId ?? proposal.AuthorUserId;
        if (notifyUserId != userId)
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = notifyUserId,
                CrewId = proposal.CrewId!.Value,
                Kind = NotificationKind.NewReply,
                Title = "New reply",
                Body = parentComment is null
                    ? "Someone commented on your proposal."
                    : "Someone replied to your proposal comment.",
                ActionUrl = actionUrl,
                RelatedEntityId = proposal.Id,
                SecondaryEntityId = comment.Id,
                ActorUserId = userId
            }, cancellationToken);
        }

        await contentMentionService.ApplyMentionsAsync(new ContentMentionContext
        {
            CrewId = proposal.CrewId!.Value,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ProposalComment,
            ResourceId = comment.Id,
            ParentResourceId = proposal.Id,
            ActionUrl = actionUrl,
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds)
        }, cancellationToken);

        string? alias = null;
        if (proposal.Kind == ProposalKind.General)
        {
            var aliasEntity = await aliasService.GetOrCreateAsync(proposal.Id, userId, cancellationToken);
            alias = aliasEntity.Nickname;
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Comment posted.",
            CommentId = comment.Id,
            Alias = alias
        };
    }
}
