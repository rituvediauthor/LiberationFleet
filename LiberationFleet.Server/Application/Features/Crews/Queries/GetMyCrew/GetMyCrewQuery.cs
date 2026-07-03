using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrew;

public record GetMyCrewQuery : IRequest<CrewOperationResponse>;

public class GetMyCrewQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IMutualAidService mutualAidService) : IRequestHandler<GetMyCrewQuery, CrewOperationResponse>
{
    public async Task<CrewOperationResponse> Handle(GetMyCrewQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new CrewOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new CrewOperationResponse { Success = false, Message = "Crew not found." };
        }

        var memberCount = await crewRepository.CountMembersAsync(crew.Id, cancellationToken);
        var monthlyGivingCapacity = await mutualAidService.GetCrewMonthlyGivingCapacityAsync(crew.Id, cancellationToken);
        return new CrewOperationResponse
        {
            Success = true,
            Message = "Crew loaded.",
            Crew = CrewMapper.MapCrew(crew, memberCount, monthlyGivingCapacity)
        };
    }
}
