using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.DeleteProposal;

public record DeleteProposalCommand(int ProposalId) : IRequest<ProposalOperationResponse>;

public class DeleteProposalCommandHandler(
    ICurrentUserService currentUser,
    IProposalRepository proposalRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteProposalCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(DeleteProposalCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new ProposalOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var proposal = await proposalRepository.GetByIdAsync(request.ProposalId, cancellationToken);
        if (proposal is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Proposal not found." };
        }

        if (proposal.AuthorUserId != userId)
        {
            return new ProposalOperationResponse { Success = false, Message = "Only the author can delete this proposal." };
        }

        proposal.IsDeleted = true;
        proposal.LastActivityAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProposalOperationResponse { Success = true, Message = "Proposal deleted." };
    }
}
