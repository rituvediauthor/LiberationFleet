using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.KickCrewmate;

public record KickCrewmateCommand(int TargetUserId, string Reason) : IRequest<CrewmateKickResponse>;

public class KickCrewmateCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    CrewmateKickProposalService kickProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<KickCrewmateCommand, CrewmateKickResponse>
{
    public async Task<CrewmateKickResponse> Handle(KickCrewmateCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateKickResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        if (viewerId == request.TargetUserId)
        {
            return new CrewmateKickResponse { Success = false, Message = "You cannot kick yourself." };
        }

        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(request.TargetUserId, viewerMembership.CrewId, cancellationToken))
        {
            return new CrewmateKickResponse { Success = false, Message = "Crewmate not found." };
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "Crewmate not found." };
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new CrewmateKickResponse { Success = false, Message = "A reason is required to submit a kick proposal." };
        }

        var kickResult = await kickProposalService.CreateFromCrewmateProfileAsync(
            viewerMembership.CrewId,
            viewerId,
            request.TargetUserId,
            targetUser.Username,
            request.Reason.Trim(),
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateKickResponse
        {
            Success = kickResult.Success,
            Message = kickResult.Message,
            ProposalId = kickResult.ProposalId
        };
    }
}
