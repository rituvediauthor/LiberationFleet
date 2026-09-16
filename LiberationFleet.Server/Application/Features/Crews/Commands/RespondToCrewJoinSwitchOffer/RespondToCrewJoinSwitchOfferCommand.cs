using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.RespondToCrewJoinSwitchOffer;

public record RespondToCrewJoinSwitchOfferCommand(int ProposalId, bool SwitchCrews)
    : IRequest<JoinRequestOperationResponse>;

public class RespondToCrewJoinSwitchOfferCommandHandler(
    ICurrentUserService currentUser,
    CrewJoinRequestProposalService joinRequestProposalService)
    : IRequestHandler<RespondToCrewJoinSwitchOfferCommand, JoinRequestOperationResponse>
{
    public async Task<JoinRequestOperationResponse> Handle(
        RespondToCrewJoinSwitchOfferCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new JoinRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var result = await joinRequestProposalService.RespondToSwitchOfferAsync(
            currentUser.UserId.Value,
            request.ProposalId,
            request.SwitchCrews,
            cancellationToken);

        return new JoinRequestOperationResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
