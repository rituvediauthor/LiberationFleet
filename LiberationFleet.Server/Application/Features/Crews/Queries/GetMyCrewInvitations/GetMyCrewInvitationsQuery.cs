using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetMyCrewInvitations;

public record GetMyCrewInvitationsQuery : IRequest<CrewInvitationListResponse>;

public class GetMyCrewInvitationsQueryHandler(
    ICurrentUserService currentUser,
    ICrewInvitationRepository invitationRepository) : IRequestHandler<GetMyCrewInvitationsQuery, CrewInvitationListResponse>
{
    public async Task<CrewInvitationListResponse> Handle(GetMyCrewInvitationsQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewInvitationListResponse { Success = false, Message = "Unauthorized." };
        }

        var items = await invitationRepository.GetPendingForInviteeAsync(currentUser.UserId.Value, cancellationToken);
        return new CrewInvitationListResponse
        {
            Success = true,
            Message = "Invitations loaded.",
            Items = items.Select(i => new CrewInvitationDto
            {
                Id = i.Id,
                CrewId = i.CrewId,
                CrewName = i.Crew.Name,
                InviterUserId = i.InviterUserId,
                InviterUsername = i.InviterUser.Username,
                Status = i.Status.ToString(),
                CreatedAt = i.CreatedAt
            }).ToList()
        };
    }
}
