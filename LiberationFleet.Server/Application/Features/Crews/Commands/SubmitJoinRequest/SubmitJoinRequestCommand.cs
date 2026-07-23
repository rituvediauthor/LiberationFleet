using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.SubmitJoinRequest;

public record SubmitJoinRequestCommand(
    int? CrewId,
    string? JoinCode,
    IReadOnlyList<int> AcceptedRuleIds,
    int? InvitationId = null) : IRequest<JoinRequestOperationResponse>;

public class SubmitJoinRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewRepository crewRepository,
    IRuleRepository ruleRepository,
    ICrewInvitationRepository invitationRepository,
    IFleetRepository fleetRepository,
    CrewJoinRequestProposalService joinRequestProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<SubmitJoinRequestCommand, JoinRequestOperationResponse>
{
    public async Task<JoinRequestOperationResponse> Handle(
        SubmitJoinRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new JoinRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        Domain.Entities.CrewInvitation? invitation = null;
        if (request.InvitationId.HasValue)
        {
            invitation = await invitationRepository.GetByIdAsync(request.InvitationId.Value, cancellationToken);
            if (invitation is null
                || invitation.InviteeUserId != userId
                || invitation.Status != CrewInvitationStatus.Pending)
            {
                return new JoinRequestOperationResponse { Success = false, Message = "Invitation not found or already handled." };
            }
        }

        var crew = invitation is not null
            ? await crewRepository.GetByIdAsync(invitation.CrewId, cancellationToken)
            : !string.IsNullOrWhiteSpace(request.JoinCode)
                ? await crewRepository.GetByJoinCodeAsync(request.JoinCode.Trim().ToUpperInvariant(), cancellationToken)
                : request.CrewId.HasValue
                    ? await crewRepository.GetByIdAsync(request.CrewId.Value, cancellationToken)
                    : null;

        if (crew is null)
        {
            return new JoinRequestOperationResponse
            {
                Success = false,
                Message = !string.IsNullOrWhiteSpace(request.JoinCode)
                    ? "No crew found with that join code"
                    : "Crew not found"
            };
        }

        if (invitation is not null && invitation.CrewId != crew.Id)
        {
            return new JoinRequestOperationResponse { Success = false, Message = "Invitation does not match this crew." };
        }

        if (crew.Privacy == CrewPrivacy.InviteOnly && invitation is null)
        {
            return new JoinRequestOperationResponse
            {
                Success = false,
                Message = PrivacyAccess.InviteOnlyJoinMessage("crew")
            };
        }

        if (PrivacyAccess.IsFleetScopedCrewPrivacy(crew.Privacy))
        {
            var targetFleet = await fleetRepository.GetFleetForCrewAsync(crew.Id, cancellationToken);
            if (targetFleet is null
                || !await fleetRepository.IsUserInFleetAsync(userId, targetFleet.Id, cancellationToken))
            {
                return new JoinRequestOperationResponse
                {
                    Success = false,
                    Message = PrivacyAccess.FleetMembersOnlyJoinMessage()
                };
            }
        }
        else if (crew.Privacy == CrewPrivacy.Private
            && invitation is null
            && string.IsNullOrWhiteSpace(request.JoinCode))
        {
            return new JoinRequestOperationResponse { Success = false, Message = "Crew not found." };
        }

        var publicRules = await ruleRepository.GetPublicByCrewIdAsync(crew.Id, cancellationToken);
        var requiredRuleIds = publicRules.Select(r => r.Id).OrderBy(id => id).ToList();
        var acceptedRuleIds = request.AcceptedRuleIds.Distinct().OrderBy(id => id).ToList();

        if (!requiredRuleIds.SequenceEqual(acceptedRuleIds))
        {
            return new JoinRequestOperationResponse
            {
                Success = false,
                Message = "You must accept all public rules before requesting to join."
            };
        }

        var result = await joinRequestProposalService.CreateJoinRequestAsync(
            userId,
            crew.Id,
            acceptedRuleIds,
            cancellationToken);

        if (result.Success && invitation is not null)
        {
            invitation.Status = CrewInvitationStatus.Accepted;
            invitation.RespondedAt = DateTime.UtcNow;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new JoinRequestOperationResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
