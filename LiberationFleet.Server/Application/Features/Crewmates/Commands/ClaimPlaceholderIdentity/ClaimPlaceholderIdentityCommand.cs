using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.ClaimPlaceholderIdentity;

public record ClaimPlaceholderIdentityCommand(int PlaceholderUserId) : IRequest<CrewmateKickResponse>;

public class ClaimPlaceholderIdentityCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ClaimPlaceholderIdentityProposalService claimProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<ClaimPlaceholderIdentityCommand, CrewmateKickResponse>
{
    public async Task<CrewmateKickResponse> Handle(
        ClaimPlaceholderIdentityCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateKickResponse { Success = false, Message = "Unauthorized." };
        }

        var claimantUserId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(claimantUserId, cancellationToken);
        if (membership is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "You are not in a crew." };
        }

        var result = await claimProposalService.CreateProposalAsync(
            membership.CrewId,
            claimantUserId,
            request.PlaceholderUserId,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateKickResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
