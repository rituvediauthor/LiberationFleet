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
    IMutualAidService mutualAidService,
    EmergencyReconciliationService reconciliationService,
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

        var emergencyRequest = await emergencyRequestRepository.GetByIdWithDetailsAsync(request.RequestId, cancellationToken);
        if (emergencyRequest is null || emergencyRequest.CrewId != membership.CrewId)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Emergency request not found." };
        }

        if (emergencyRequest.Status == EmergencyRequestStatus.Cancelled)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "This emergency request is no longer open." };
        }

        if (giverId == emergencyRequest.RequesterUserId)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You cannot give to your own emergency request." };
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

        var reconciliation = await reconciliationService.ApplyDirectGiftAsync(
            emergencyRequest,
            request.Amount,
            cancellationToken);

        Gift? emergencyGift = null;
        if (reconciliation.AmountAppliedToNeed > 0m)
        {
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

            emergencyGift = CreateEmergencyGift(
                membership.CrewId,
                giverId,
                emergencyRequest.RequesterUserId,
                reconciliation.AmountAppliedToNeed,
                request.PaymentPlatformId,
                request.MiddlemanId,
                emergencyRequest.Id,
                seasonCycleId);

            await giftRepository.AddAsync(emergencyGift, cancellationToken);
            await emergencyRequestRepository.AddGiftResponseAsync(new EmergencyGiftResponse
            {
                EmergencyRequest = emergencyRequest,
                GiverUserId = giverId,
                Gift = emergencyGift,
                Amount = reconciliation.AmountAppliedToNeed,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        if (reconciliation.OverflowAmount > 0m)
        {
            var overflowGift = CreateUncategorizedGift(
                membership.CrewId,
                giverId,
                emergencyRequest.RequesterUserId,
                reconciliation.OverflowAmount,
                request.PaymentPlatformId);
            await giftRepository.AddAsync(overflowGift, cancellationToken);
        }

        await mutualAidService.RecordEmergencySacrificeAsync(membership.CrewId, giverId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (emergencyGift?.CountsTowardReception == true)
        {
            await mutualAidService.ApplyGiftReceptionAsync(emergencyGift, cancellationToken);
        }

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = reconciliation.OverflowAmount > 0m
                ? "Emergency gift recorded; excess amount logged as an uncategorized gift."
                : "Emergency gift recorded.",
            RequestId = emergencyRequest.Id
        };
    }

    private static Gift CreateEmergencyGift(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        int paymentPlatformId,
        int? middlemanId,
        int emergencyRequestId,
        int? seasonCycleId) =>
        new()
        {
            CrewId = crewId,
            GiverUserId = giverUserId,
            RecipientUserId = recipientUserId,
            MiddlemanUserId = middlemanId,
            Type = middlemanId.HasValue ? GiftType.Initiated : GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatformId = paymentPlatformId,
            IsSurvivalThreshold = false,
            IsCustomGift = true,
            CountsTowardReception = !middlemanId.HasValue,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Verified,
            EmergencyRequestId = emergencyRequestId,
            SeasonCycleId = seasonCycleId,
            CreatedAt = DateTime.UtcNow
        };

    private static Gift CreateUncategorizedGift(
        int crewId,
        int giverUserId,
        int recipientUserId,
        decimal amount,
        int paymentPlatformId) =>
        new()
        {
            CrewId = crewId,
            GiverUserId = giverUserId,
            RecipientUserId = recipientUserId,
            Type = GiftType.Direct,
            Amount = amount,
            CrewPaymentPlatformId = paymentPlatformId,
            IsSurvivalThreshold = false,
            IsCustomGift = true,
            CountsTowardReception = false,
            CountsTowardContribution = false,
            VerificationStatus = GiftVerificationStatus.Verified,
            CreatedAt = DateTime.UtcNow
        };
}
