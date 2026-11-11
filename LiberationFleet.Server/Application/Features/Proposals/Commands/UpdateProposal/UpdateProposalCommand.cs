using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Proposals;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.UpdateProposal;

public record UpdateProposalCommand(
    int ProposalId,
    string Nonce,
    string Ciphertext,
    int KeyVersion,
    IReadOnlyList<int> MentionedUserIds) : IRequest<ProposalOperationResponse>;

public class UpdateProposalCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    ContentMentionService contentMentionService,
    IUnitOfWork unitOfWork) : IRequestHandler<UpdateProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(UpdateProposalCommand request, CancellationToken cancellationToken)
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

        if (proposal.AuthorUserId != userId)
        {
            return new ProposalOperationResponse { Success = false, Message = "Only the author can edit this proposal." };
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
        proposal.LastActivityAt = DateTime.UtcNow;

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.Proposal,
            ResourceId = proposal.Id.ToString(),
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
            OmitNotificationActorIdentity = true,
            NotificationAuthorName = ProposalMapper.AnonymousAuthor,
            ContentType = MentionedContentType.Proposal,
            ResourceId = proposal.Id,
            ActionUrl = ProposalRouting.StatusListUrl(proposal),
            MentionedUserIds = MentionRequestHelper.Normalize(request.MentionedUserIds),
            IsUpdate = true
        }, cancellationToken);

        return new ProposalOperationResponse { Success = true, Message = "Proposal updated." };
    }
}
