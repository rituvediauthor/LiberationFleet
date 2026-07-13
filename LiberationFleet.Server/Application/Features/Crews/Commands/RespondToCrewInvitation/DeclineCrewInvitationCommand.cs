using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.RespondToCrewInvitation;

public record DeclineCrewInvitationCommand(int InvitationId) : IRequest<CrewInvitationOperationResponse>;

public class DeclineCrewInvitationCommandHandler(
    ICurrentUserService currentUser,
    ICrewInvitationRepository invitationRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<DeclineCrewInvitationCommand, CrewInvitationOperationResponse>
{
    public async Task<CrewInvitationOperationResponse> Handle(
        DeclineCrewInvitationCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var invitation = await invitationRepository.GetByIdAsync(request.InvitationId, cancellationToken);
        if (invitation is null || invitation.InviteeUserId != currentUser.UserId.Value)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "Invitation not found." };
        }

        if (invitation.Status != CrewInvitationStatus.Pending)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "This invitation is no longer pending." };
        }

        invitation.Status = CrewInvitationStatus.Declined;
        invitation.RespondedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewInvitationOperationResponse
        {
            Success = true,
            Message = "Invitation declined.",
            InvitationId = invitation.Id
        };
    }
}
