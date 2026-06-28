using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.AllowCrewmateRejoin;

public record AllowCrewmateRejoinCommand(int TargetUserId) : IRequest<CrewmateKickResponse>;

public class AllowCrewmateRejoinCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    CrewmateRejoinProposalService rejoinProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<AllowCrewmateRejoinCommand, CrewmateKickResponse>
{
    public async Task<CrewmateKickResponse> Handle(
        AllowCrewmateRejoinCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateKickResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "You are not in a crew." };
        }

        var targetMembership = await membershipRepository.GetMembershipAsync(
            request.TargetUserId,
            viewerMembership.CrewId,
            cancellationToken);
        if (targetMembership is null || !targetMembership.IsBanned)
        {
            return new CrewmateKickResponse { Success = false, Message = "That crewmate is not currently kicked from the crew." };
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "Crewmate not found." };
        }

        var result = await rejoinProposalService.CreateProposalAsync(
            viewerMembership.CrewId,
            viewerId,
            request.TargetUserId,
            targetUser.Username,
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
