using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.KickCrewmateFromSeason;

public record KickCrewmateFromSeasonCommand(int TargetUserId, string Reason) : IRequest<CrewmateKickResponse>;

public class KickCrewmateFromSeasonCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    CrewmateKickProposalService kickProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<KickCrewmateFromSeasonCommand, CrewmateKickResponse>
{
    public async Task<CrewmateKickResponse> Handle(
        KickCrewmateFromSeasonCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateKickResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        if (viewerId == request.TargetUserId)
        {
            return new CrewmateKickResponse { Success = false, Message = "You cannot remove yourself from the season this way." };
        }

        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "You are not in a crew." };
        }

        var targetMembership = await membershipRepository.GetMembershipAsync(
            request.TargetUserId,
            viewerMembership.CrewId,
            cancellationToken);
        if (targetMembership is null || targetMembership.IsBanned)
        {
            return new CrewmateKickResponse { Success = false, Message = "Crewmate not found." };
        }

        if (!targetMembership.IsInSeason)
        {
            return new CrewmateKickResponse { Success = false, Message = "That crewmate is not in the current season." };
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "Crewmate not found." };
        }

        if (string.IsNullOrWhiteSpace(request.Reason))
        {
            return new CrewmateKickResponse
            {
                Success = false,
                Message = "A reason is required to submit a season-removal proposal."
            };
        }

        var kickResult = await kickProposalService.CreateSeasonKickFromCrewmateProfileAsync(
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
