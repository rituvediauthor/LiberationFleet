using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.RecordEmergencyGift;

public record RecordEmergencyGiftCommand(
    int RequestId,
    decimal Amount,
    int PaymentPlatformId,
    int? MiddlemanId) : IRequest<EmergencyRequestOperationResponse>;

public class RecordEmergencyGiftCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IGiftRepository giftRepository,
    IMutualAidRepository mutualAidRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<RecordEmergencyGiftCommand, EmergencyRequestOperationResponse>
{
    public async Task<EmergencyRequestOperationResponse> Handle(
        RecordEmergencyGiftCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        if (request.Amount <= 0)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Gift amount must be greater than zero." };
        }

        var giverId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(giverId, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You must be in an active season to record a gift." };
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
            return new EmergencyRequestOperationResponse { Success = false, Message = "You cannot give to your own emergency request." };
        }

        var remaining = emergencyRequest.AmountNeeded - emergencyRequest.AmountFulfilled;
        if (request.Amount > remaining)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Gift amount exceeds the remaining emergency need." };
        }

        if (!await crewPaymentPlatformRepository.ExistsForCrewAsync(membership.CrewId, request.PaymentPlatformId, cancellationToken))
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Invalid payment platform." };
        }

        if (request.MiddlemanId.HasValue
            && !await membershipRepository.IsUserInCrewAsync(request.MiddlemanId.Value, membership.CrewId, cancellationToken))
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Middleman is not in your crew." };
        }

        int? seasonCycleId = null;
        var crew = await mutualAidRepository.GetCrewAsync(membership.CrewId, cancellationToken);
        if (crew?.CurrentSeasonStartDate is not null)
        {
            var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
                membership.CrewId,
                crew.CurrentSeasonStartDate.Value,
                cancellationToken);
            seasonCycleId = cycles
                .Where(c => c.EmergencyRequestId == emergencyRequest.Id && !c.CycleCompleted)
                .OrderBy(c => c.ReceptionOrderPosition)
                .FirstOrDefault()?.Id;
        }

        var gift = new Gift
        {
            CrewId = membership.CrewId,
            GiverUserId = giverId,
            RecipientUserId = emergencyRequest.RequesterUserId,
            MiddlemanUserId = request.MiddlemanId,
            Type = request.MiddlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
            Amount = request.Amount,
            CrewPaymentPlatformId = request.PaymentPlatformId,
            IsSurvivalThreshold = false,
            IsCustomGift = true,
            CountsTowardReception = !request.MiddlemanId.HasValue,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Verified,
            EmergencyRequestId = emergencyRequest.Id,
            SeasonCycleId = seasonCycleId,
            CreatedAt = DateTime.UtcNow
        };

        await giftRepository.AddAsync(gift, cancellationToken);
        await emergencyRequestRepository.AddGiftResponseAsync(new EmergencyGiftResponse
        {
            EmergencyRequest = emergencyRequest,
            GiverUserId = giverId,
            Gift = gift,
            Amount = request.Amount,
            CreatedAt = DateTime.UtcNow
        }, cancellationToken);

        emergencyRequest.AmountFulfilled += request.Amount;
        if (emergencyRequest.AmountFulfilled >= emergencyRequest.AmountNeeded)
        {
            emergencyRequest.Status = EmergencyRequestStatus.Fulfilled;
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = "Emergency gift recorded.",
            RequestId = emergencyRequest.Id
        };
    }
}
