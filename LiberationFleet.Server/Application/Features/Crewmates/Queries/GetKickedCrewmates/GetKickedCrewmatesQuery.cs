using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Queries.GetKickedCrewmates;

public record GetKickedCrewmatesQuery : IRequest<KickedCrewmateListResponse>;

public class GetKickedCrewmatesQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository) : IRequestHandler<GetKickedCrewmatesQuery, KickedCrewmateListResponse>
{
    public async Task<KickedCrewmateListResponse> Handle(
        GetKickedCrewmatesQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new KickedCrewmateListResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            return new KickedCrewmateListResponse { Success = false, Message = "You are not in a crew." };
        }

        var bannedMembers = await membershipRepository.GetBannedMembersByCrewIdAsync(membership.CrewId, cancellationToken);
        var items = bannedMembers
            .Select(m => new KickedCrewmateListItemDto
            {
                UserId = m.UserId,
                Username = m.User.Username
            })
            .ToList();

        return new KickedCrewmateListResponse
        {
            Success = true,
            Message = items.Count > 0 ? "Kicked crewmates loaded." : "No kicked crewmates.",
            Items = items
        };
    }
}
