using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.UpdateProposalComment;

public record UpdateProposalCommentCommand(
    int ProposalId,
    int CommentId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds,
    string? Body) : IRequest<ProposalOperationResponse>;

public class UpdateProposalCommentCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProposalCommentCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(UpdateProposalCommentCommand request, CancellationToken cancellationToken)
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

        var comment = await proposalRepository.GetCommentByIdAsync(request.CommentId, cancellationToken);
        if (comment is null || comment.ProposalId != proposal.Id)
        {
            return new ProposalOperationResponse { Success = false, Message = "Comment not found." };
        }

        if (comment.AuthorUserId != userId)
        {
            return new ProposalOperationResponse { Success = false, Message = "Only the author can edit this comment." };
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

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProposalOperationResponse { Success = false, Message = "Encrypted comment content is required." };
        }

        var isFleetProposal = proposal.FleetId.HasValue;
        proposal.LastActivityAt = DateTime.UtcNow;
        comment.Body = null;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.ProposalComment,
            ResourceId = comment.Id.ToString(),
            CrewId = isFleetProposal ? null : proposal.CrewId,
            FleetId = isFleetProposal ? proposal.FleetId : null,
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
            CrewId = membership.CrewId,
            FleetId = proposal.FleetId,
            AuthorUserId = userId,
            ContentType = MentionedContentType.ProposalComment,
            ResourceId = comment.Id,
            ParentResourceId = proposal.Id,
            ActionUrl = ProposalRouting.CommentUrl(proposal, comment.Id),
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            IsUpdate = true
        }, cancellationToken);

        return new ProposalOperationResponse { Success = true, Message = "Comment updated.", CommentId = comment.Id };
    }
}
