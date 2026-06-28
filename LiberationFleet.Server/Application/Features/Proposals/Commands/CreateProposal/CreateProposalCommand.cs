using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.CreateProposal;

public record CreateProposalCommand(
    string Nonce,
    string Ciphertext,
    int KeyVersion) : IRequest<ProposalOperationResponse>;

public class CreateProposalCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    ICryptoRepository cryptoRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(CreateProposalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (string.IsNullOrWhiteSpace(request.Nonce) || string.IsNullOrWhiteSpace(request.Ciphertext))
        {
            return new ProposalOperationResponse { Success = false, Message = "Encrypted proposal content is required." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var utcNow = DateTime.UtcNow;
        var proposal = new Proposal
        {
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            CreatedAt = utcNow,
            LastActivityAt = utcNow
        };
        ProposalVotingService.ApplyTimerRulesOnCreate(proposal, utcNow);

        await proposalRepository.AddProposalAsync(proposal, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await cryptoRepository.UpsertEnvelopeAsync(new EncryptedContentEnvelope
        {
            ContentType = EncryptedContentType.Proposal,
            ResourceId = proposal.Id.ToString(),
            CrewId = membership.CrewId,
            AuthorUserId = userId,
            KeyVersion = request.KeyVersion <= 0 ? 1 : request.KeyVersion,
            Nonce = request.Nonce.Trim(),
            Ciphertext = request.Ciphertext.Trim(),
            CreatedAt = utcNow,
            UpdatedAt = utcNow
        }, cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyCrewAsync(
            membership.CrewId,
            NotificationKind.NewProposal,
            "New proposal",
            "A new crew proposal was submitted.",
            $"/app/crew/proposals/{proposal.Id}",
            relatedEntityId: proposal.Id,
            excludeUserId: userId,
            cancellationToken: cancellationToken);

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Proposal created.",
            ProposalId = proposal.Id
        };
    }
}
