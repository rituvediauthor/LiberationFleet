using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.SubmitEmergencySplit;

public record SubmitEmergencySplitCommand(int RequestId, decimal Amount)
    : IRequest<EmergencyRequestOperationResponse>;

public class SubmitEmergencySplitCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    EmergencySplitService splitService,
    IUnitOfWork unitOfWork) : IRequestHandler<SubmitEmergencySplitCommand, EmergencyRequestOperationResponse>
{
    public async Task<EmergencyRequestOperationResponse> Handle(
        SubmitEmergencySplitCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You must be in an active season to split a cycle." };
        }

        var emergencyRequest = await emergencyRequestRepository.GetByIdAsync(request.RequestId, cancellationToken);
        if (emergencyRequest is null || emergencyRequest.CrewId != membership.CrewId)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Emergency request not found." };
        }

        if (emergencyRequest.Status != EmergencyRequestStatus.Open)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "This emergency request is no longer open." };
        }

        if (emergencyRequest.RequesterUserId == currentUser.UserId.Value)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You cannot split your own emergency request." };
        }

        var result = await splitService.ApplySplitAsync(
            emergencyRequest,
            currentUser.UserId.Value,
            request.Amount,
            cancellationToken);

        if (!result.Success)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = result.Message };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = result.Message,
            RequestId = emergencyRequest.Id
        };
    }
}
