using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Fleets.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Fleets.Queries.LookupCrewByJoinCode;

public record LookupCrewByJoinCodeQuery(string JoinCode) : IRequest<CrewLookupResponse>;

public class LookupCrewByJoinCodeQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IFleetRepository fleetRepository) : IRequestHandler<LookupCrewByJoinCodeQuery, CrewLookupResponse>
{
    public async Task<CrewLookupResponse> Handle(LookupCrewByJoinCodeQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewLookupResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewLookupResponse { Success = false, Message = "You must be in a crew." };
        }

        if (await fleetRepository.GetFleetForCrewAsync(membership.CrewId, cancellationToken) is null)
        {
            return new CrewLookupResponse { Success = false, Message = "Your crew is not in a fleet." };
        }

        var joinCode = (request.JoinCode ?? string.Empty).Trim().ToUpperInvariant();
        if (string.IsNullOrWhiteSpace(joinCode))
        {
            return new CrewLookupResponse { Success = false, Message = "Join code is required." };
        }

        var crew = await crewRepository.GetByJoinCodeAsync(joinCode, cancellationToken);
        if (crew is null)
        {
            return new CrewLookupResponse { Success = false, Message = "No crew found with that join code." };
        }

        var members = await membershipRepository.GetActiveMembersByCrewIdAsync(crew.Id, cancellationToken);
        var alreadyInFleet = await fleetRepository.GetFleetForCrewAsync(crew.Id, cancellationToken) is not null;

        return new CrewLookupResponse
        {
            Success = true,
            Message = "Crew found.",
            Crew = new CrewLookupDto
            {
                CrewId = crew.Id,
                CrewName = crew.Name,
                MemberCount = members.Count,
                AlreadyInFleet = alreadyInFleet,
                IsOwnCrew = crew.Id == membership.CrewId
            }
        };
    }
}
