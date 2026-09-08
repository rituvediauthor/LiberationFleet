using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.CancelEmergencyRequest;

public record CancelEmergencyRequestCommand(int RequestId) : IRequest<EmergencyRequestOperationResponse>;

public class CancelEmergencyRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<CancelEmergencyRequestCommand, EmergencyRequestOperationResponse>
{
    public async Task<EmergencyRequestOperationResponse> Handle(
        CancelEmergencyRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You must be in a crew to close an emergency request." };
        }

        var emergencyRequest = await emergencyRequestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (emergencyRequest is null || emergencyRequest.CrewId != membership.CrewId)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Emergency request not found." };
        }

        if (emergencyRequest.RequesterUserId != userId)
        {
            return new EmergencyRequestOperationResponse
            {
                Success = false,
                Message = "Only the requester can close this emergency request."
            };
        }

        if (emergencyRequest.Status != EmergencyRequestStatus.Open)
        {
            return new EmergencyRequestOperationResponse
            {
                Success = false,
                Message = "This emergency request is no longer open."
            };
        }

        // Keep split segments, paybacks, and queue-funded gifts intact; only stop new
        // direct gifts/splits and hide the request from open lists.
        emergencyRequest.Status = EmergencyRequestStatus.Cancelled;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = "Emergency request closed.",
            RequestId = emergencyRequest.Id
        };
    }
}
