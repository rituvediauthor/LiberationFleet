using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Notifications.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.InviteCrewmate;

public record InviteCrewmateCommand(int InviteeUserId) : IRequest<CrewInvitationOperationResponse>;

public class InviteCrewmateCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ICrewRepository crewRepository,
    IUserRepository userRepository,
    ICrewInvitationRepository invitationRepository,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<InviteCrewmateCommand, CrewInvitationOperationResponse>
{
    public async Task<CrewInvitationOperationResponse> Handle(
        InviteCrewmateCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var inviterId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(inviterId, cancellationToken);
        if (membership is null)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (request.InviteeUserId == inviterId)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "You cannot invite yourself." };
        }

        var invitee = await userRepository.GetByIdWithProfileAsync(request.InviteeUserId, cancellationToken);
        if (invitee is null || invitee.IsUnclaimedPlaceholder)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "User not found." };
        }

        if (await membershipRepository.GetActiveMembershipAsync(request.InviteeUserId, cancellationToken) is not null)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "That user is already in a crew." };
        }

        if (await invitationRepository.GetPendingAsync(membership.CrewId, request.InviteeUserId, cancellationToken) is not null)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "An invitation is already pending for that user." };
        }

        var crew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
        if (crew is null)
        {
            return new CrewInvitationOperationResponse { Success = false, Message = "Crew not found." };
        }

        var invitation = new CrewInvitation
        {
            CrewId = membership.CrewId,
            InviterUserId = inviterId,
            InviteeUserId = request.InviteeUserId,
            Status = CrewInvitationStatus.Pending,
            CreatedAt = DateTime.UtcNow
        };

        await invitationRepository.AddAsync(invitation, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        await notificationService.NotifyUserAsync(new CreateNotificationRequest
        {
            UserId = request.InviteeUserId,
            CrewId = membership.CrewId,
            Kind = NotificationKind.JoinRequestFromCrew,
            Title = "Crew invitation",
            Body = $"You were invited to join {crew.Name}.",
            ActionUrl = $"/app/crew/invitations/{invitation.Id}",
            RelatedEntityId = invitation.Id,
            ActorUserId = inviterId
        }, cancellationToken);

        return new CrewInvitationOperationResponse
        {
            Success = true,
            Message = "Invitation sent.",
            InvitationId = invitation.Id
        };
    }
}
