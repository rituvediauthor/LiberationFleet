using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Proposals.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Proposals.Commands.RerollProposalAlias;

public record RerollProposalAliasCommand(int ProposalId) : IRequest<ProposalOperationResponse>;

public class RerollProposalAliasCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
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

        if (proposal.Kind != ProposalKind.General)
        {
            return new ProposalOperationResponse { Success = false, Message = "Nicknames are only used on general proposals." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(userId, proposal.CrewId, cancellationToken))
        {
            return new ProposalOperationResponse { Success = false, Message = "You are not in this crew." };
        }

        var alias = await aliasService.GetOrCreateAsync(proposal.Id, userId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var rerolled = await aliasService.RerollAsync(proposal.Id, userId, cancellationToken);
        if (rerolled is null)
        {
            return new ProposalOperationResponse { Success = false, Message = "Could not reroll nickname." };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new ProposalOperationResponse
        {
            Success = true,
            Message = "Nickname updated.",
            Alias = rerolled.Nickname
        };
    }
}
