using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.MarkEmergencyGiftAlreadyLogged;

public record MarkEmergencyGiftAlreadyLoggedCommand(int RequestId, decimal Amount)
    : IRequest<EmergencyRequestOperationResponse>;

public class MarkEmergencyGiftAlreadyLoggedCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<MarkEmergencyGiftAlreadyLoggedCommand, EmergencyRequestOperationResponse>
{
    public async Task<EmergencyRequestOperationResponse> Handle(
        MarkEmergencyGiftAlreadyLoggedCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.Amount <= 0)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Amount must be greater than zero." };
        }

        var giverId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(giverId, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You must be in an active season." };
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

        if (giverId == emergencyRequest.RequesterUserId)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You cannot respond to your own emergency request." };
        }

        var remaining = emergencyRequest.AmountNeeded - emergencyRequest.AmountFulfilled;
        if (request.Amount > remaining)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Amount exceeds the remaining emergency need." };
        }

        await emergencyRequestRepository.AddGiftResponseAsync(new EmergencyGiftResponse
        {
            EmergencyRequest = emergencyRequest,
            GiverUserId = giverId,
            GiftId = null,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        emergencyRequest.AmountFulfilled += request.Amount;
        if (emergencyRequest.AmountFulfilled >= emergencyRequest.AmountNeeded)
        {
            emergencyRequest.Status = EmergencyRequestStatus.Fulfilled;
        }

        await mutualAidService.RecordEmergencySacrificeAsync(membership.CrewId, giverId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = "Logged prior gift toward this emergency request.",
            RequestId = emergencyRequest.Id
        };
    }
}
