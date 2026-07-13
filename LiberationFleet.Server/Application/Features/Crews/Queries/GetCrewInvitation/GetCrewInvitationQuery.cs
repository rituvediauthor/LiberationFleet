using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Queries.GetCrewInvitation;

public record GetCrewInvitationQuery(int InvitationId) : IRequest<CrewInvitationDetailResponse>;

public class GetCrewInvitationQueryHandler(
    ICurrentUserService currentUser,
    ICrewInvitationRepository invitationRepository) : IRequestHandler<GetCrewInvitationQuery, CrewInvitationDetailResponse>
{
    public async Task<CrewInvitationDetailResponse> Handle(GetCrewInvitationQuery request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewInvitationDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var invitation = await invitationRepository.GetByIdAsync(request.InvitationId, cancellationToken);
        if (invitation is null || invitation.InviteeUserId != currentUser.UserId.Value)
        {
            return new CrewInvitationDetailResponse { Success = false, Message = "Invitation not found." };
        }

        return new CrewInvitationDetailResponse
        {
            Success = true,
            Message = "Invitation loaded.",
            Invitation = new CrewInvitationDto
            {
                Id = invitation.Id,
                CrewId = invitation.CrewId,
                CrewName = invitation.Crew.Name,
                InviterUserId = invitation.InviterUserId,
                InviterUsername = invitation.InviterUser.Username,
                Status = invitation.Status.ToString(),
                CreatedAt = invitation.CreatedAt
            }
        };
    }
}
