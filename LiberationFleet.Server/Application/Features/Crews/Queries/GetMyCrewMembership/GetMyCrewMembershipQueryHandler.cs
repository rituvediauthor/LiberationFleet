using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewMembership;

public class GetMyCrewMembershipQueryHandler : IRequestHandler<GetMyCrewMembershipQuery, CrewMembershipStatusDto>
{
    private readonly ICrewMembershipRepository _membershipRepository;
    private readonly ICurrentUserService _currentUserService;

    public GetMyCrewMembershipQueryHandler(
        ICrewMembershipRepository membershipRepository,
        ICurrentUserService currentUserService)
    {
        _membershipRepository = membershipRepository;
        _currentUserService = currentUserService;
    }

    public async Task<CrewMembershipStatusDto> Handle(GetMyCrewMembershipQuery request, CancellationToken cancellationToken)
    {
        var userId = _currentUserService.UserId;
        if (userId is null)
        {
            return new CrewMembershipStatusDto();
        }

        var membership = await _membershipRepository.GetActiveMembershipAsync(userId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewMembershipStatusDto { HasCrew = false };
        }

        return new CrewMembershipStatusDto
        {
            HasCrew = true,
            CrewId = membership.CrewId,
            CrewName = membership.Crew.Name,
            JoinCode = membership.Crew.JoinCode
        };
    }
}
