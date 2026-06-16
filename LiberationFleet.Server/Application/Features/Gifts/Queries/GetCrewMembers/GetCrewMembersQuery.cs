using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Gifts.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Gifts.Queries.GetCrewMembers;

public record GetCrewMembersQuery : IRequest<IReadOnlyList<CrewMemberDto>>;

public class GetCrewMembersQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository) : IRequestHandler<GetCrewMembersQuery, IReadOnlyList<CrewMemberDto>>
{
    public async Task<IReadOnlyList<CrewMemberDto>> Handle(GetCrewMembersQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Array.Empty<CrewMemberDto>();
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return Array.Empty<CrewMemberDto>();
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(membership.CrewId, cancellationToken);

        return members
            .Select(m => new CrewMemberDto
            {
                Id = m.UserId,
                Username = m.User.Username
            })
            .OrderBy(m => m.Username)
            .ToList();
    }
}
