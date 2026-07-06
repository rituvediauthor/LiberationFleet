using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Queries.GetCrewRoleDefinitions;

public record GetCrewRoleDefinitionsQuery : IRequest<CrewRoleDefinitionsResponse>;

public class GetCrewRoleDefinitionsQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository) : IRequestHandler<GetCrewRoleDefinitionsQuery, CrewRoleDefinitionsResponse>
{
    public async Task<CrewRoleDefinitionsResponse> Handle(GetCrewRoleDefinitionsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewRoleDefinitionsResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewRoleDefinitionsResponse { Success = false, Message = "You are not in a crew." };
        }

        return new CrewRoleDefinitionsResponse
        {
            Success = true,
            Message = "Roles loaded.",
            Roles = CrewRoleMapper.GetAllRoleDefinitions()
        };
    }
}
