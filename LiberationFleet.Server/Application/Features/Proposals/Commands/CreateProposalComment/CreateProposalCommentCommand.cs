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
    string? Body,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds) : IRequest<ProposalOperationResponse>;

public class CreateProposalCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
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

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        var (allowed, accessError) = await ProposalEligibility.CanUserAccessProposalAsync(
            userId,
            proposal,
            membershipRepository,
            fleetRepository,
            cancellationToken);
        if (!allowed)
        {
            return new ProposalOperationResponse { Success = false, Message = accessError ?? "Access denied." };
        }

        var isFleetProposal = proposal.FleetId.HasValue;
        if (isFleetProposal)
        {
            if (string.IsNullOrWhiteSpace(request.Body))
            {
                return new ProposalOperationResponse { Success = false, Message = "Comment body is required." };
            }
        }
        else if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProposalOperationResponse { Success = false, Message = "Encrypted comment content is required." };
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
            Body = isFleetProposal ? request.Body!.Trim() : null,
            CreatedAt = utcNow
        };

        await proposalRepository.AddCommentAsync(comment, cancellationToken);
        proposal.LastActivityAt = utcNow;

        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (!isFleetProposal)
        {
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
        }

        var actionUrl = ProposalRouting.CommentUrl(proposal, comment.Id);
        var notifyCrewId = proposal.CrewId ?? membership.CrewId;
        var notifyUserId = parentComment?.AuthorUserId ?? proposal.AuthorUserId;
        if (notifyUserId != userId)
        {
            await notificationService.NotifyUserAsync(new CreateNotificationRequest
            {
                UserId = notifyUserId,
                CrewId = notifyCrewId,
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
            CrewId = membership.CrewId,
            FleetId = proposal.FleetId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ProposalComment,
            ResourceId = comment.Id,
            ParentResourceId = proposal.Id,
            ActionUrl = actionUrl,
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds)
        }, cancellationToken);

        string? alias = null;
        if (proposal.Kind == ProposalKind.General && !isFleetProposal)
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
