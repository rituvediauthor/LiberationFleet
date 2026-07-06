using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.DemoteCrewRoles;

public record DemoteCrewRolesCommand(int TargetUserId, IReadOnlyList<string> Roles) : IRequest<CrewRoleChangeResponse>;

public class DemoteCrewRolesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    CrewRoleProposalService roleProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<DemoteCrewRolesCommand, CrewRoleChangeResponse>
{
    public async Task<CrewRoleChangeResponse> Handle(DemoteCrewRolesCommand request, CancellationToken cancellationToken)
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
        var result = await roleProposalService.CreateDemotionAsync(
            viewerMembership.CrewId,
            viewerId,
            request.TargetUserId,
            roles,
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
