using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.NominateCrewRoles;

public record NominateCrewRolesCommand(
    int TargetUserId,
    IReadOnlyList<string> Roles,
    DateTime? RepresentativeTermStartUtc = null,
    DateTime? RepresentativeTermEndUtc = null) : IRequest<CrewRoleChangeResponse>;

public class NominateCrewRolesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    CrewRoleProposalService roleProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<NominateCrewRolesCommand, CrewRoleChangeResponse>
{
    public async Task<CrewRoleChangeResponse> Handle(NominateCrewRolesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(request.TargetUserId, viewerMembership.CrewId, cancellationToken))
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Crewmate not found." };
        }

        var roles = CrewRoleMapper.ParseRoles(request.Roles);
        var result = await roleProposalService.CreateNominationAsync(
            viewerMembership.CrewId,
            viewerId,
            request.TargetUserId,
            roles,
            request.RepresentativeTermStartUtc,
            request.RepresentativeTermEndUtc,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewRoleChangeResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
