using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.VoteProposal;

public record VoteProposalCommand(int ProposalId, string Vote) : IRequest<ProposalOperationResponse>;

public class VoteProposalCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IProposalRepository proposalRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<VoteProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(VoteProposalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var isApprove = request.Vote.Equals("approve", StringComparison.OrdinalIgnoreCase);
        var isDisapprove = request.Vote.Equals("disapprove", StringComparison.OrdinalIgnoreCase);
        if (!isApprove && !isDisapprove)
        {
            return new ProposalOperationResponse { Success = false, Message = "Vote must be approve or disapprove." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId, cancellationToken))
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var utcNow = DateTime.UtcNow;
        var existingVote = await proposalRepository.GetVoteAsync(proposal.Id, userId, cancellationToken);

        if (existingVote is not null)
        {
            if (existingVote.IsApprove == isApprove)
            {
                return new ProposalOperationResponse { Success = true, Message = "Vote unchanged." };
            }

            if (existingVote.IsApprove)
            {
                proposal.ApproveCount = Math.Max(0, proposal.ApproveCount - 1);
            }
            else
            {
                proposal.DisapproveCount = Math.Max(0, proposal.DisapproveCount - 1);
            }

            proposalRepository.RemoveVote(existingVote);
        }

        await proposalRepository.AddVoteAsync(new ProposalVote
        {
            ProposalId = proposal.Id,
            UserId = userId,
            IsApprove = isApprove,
            VotedAt = utcNow
        }, cancellationToken);

        if (isApprove)
        {
            proposal.ApproveCount++;
        }
        else
        {
            proposal.DisapproveCount++;
            ProposalVotingService.ApplyDisapproveTimerExtension(proposal, utcNow);
        }

        proposal.LastActivityAt = utcNow;
        var eligibleCount = await proposalRepository.GetActiveCrewMemberCountAsync(proposal.CrewId, cancellationToken);
        ProposalVotingService.RecalculateStatus(proposal, eligibleCount, utcNow);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Vote recorded."
        };
    }
}
