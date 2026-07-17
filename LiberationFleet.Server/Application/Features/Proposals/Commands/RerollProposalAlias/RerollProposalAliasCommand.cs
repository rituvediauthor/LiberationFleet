using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.RerollProposalAlias;

public record RerollProposalAliasCommand(int ProposalId) : IRequest<ProposalOperationResponse>;

public class RerollProposalAliasCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IProposalRepository proposalRepository,
    ProposalAnonymousAliasService aliasService,
    IUnitOfWork unitOfWork) : IRequestHandler<RerollProposalAliasCommand, ProposalOperationResponse>
{
    public async Task<ProposalOperationResponse> Handle(
        RerollProposalAliasCommand request,
        CancellationToken cancellationToken)
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

        await aliasService.GetOrCreateAsync(proposal.Id, userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var (rerolled, error) = await aliasService.TryRerollAsync(proposal.Id, userId, cancellationToken);
        if (rerolled is null)
        {
            return new ProposalOperationResponse
            {
                Success = false,
                Message = error ?? "Could not regenerate alias.",
                AliasRerollsRemaining = 0
            };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Alias updated.",
            Alias = rerolled.Nickname,
            AliasRerollsRemaining = rerolled.RerollsRemaining
        };
    }
}
