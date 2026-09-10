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
    ICrewRepository crewRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IFleetRepository fleetRepository,
    ICrewPaymentPlatformRepository crewPaymentPlatformRepository,
    IGiftRepository giftRepository,
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

        var (emergencyRequest, accessError) = await EmergencyRequestAccess.GetAccessibleRequestAsync(
            emergencyRequestRepository,
            fleetRepository,
            request.RequestId,
            membership.CrewId,
            cancellationToken,
            withDetails: true);
        if (emergencyRequest is null)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = accessError ?? "Emergency request not found." };
        }

        if (emergencyRequest.Status != EmergencyRequestStatus.Open)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "This emergency request is no longer open." };
        }

        if (giverId == emergencyRequest.RequesterUserId)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "You cannot give to your own emergency request." };
        }

        var requestCrewId = emergencyRequest.CrewId;
        if (membership.CrewId != requestCrewId)
        {
            var giverCrew = await crewRepository.GetByIdAsync(membership.CrewId, cancellationToken);
            if (giverCrew is null || !giverCrew.AllowCrossCrewGiving)
            {
                return new EmergencyRequestOperationResponse
                {
                    Success = false,
                    Message = "Your crew does not allow giving aid to other crews in the fleet."
                };
            }
        }

        var platformOk = await crewPaymentPlatformRepository.ExistsForCrewAsync(
                requestCrewId,
                request.PaymentPlatformId,
                cancellationToken)
            || await crewPaymentPlatformRepository.ExistsForCrewAsync(
                membership.CrewId,
                request.PaymentPlatformId,
                cancellationToken);
        if (!platformOk)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Invalid payment platform." };
        }

        if (request.MiddlemanId.HasValue)
        {
            var middlemanInRequestCrew = await membershipRepository.IsUserInCrewAsync(
                request.MiddlemanId.Value,
                requestCrewId,
                cancellationToken);
            var middlemanInGiverCrew = membership.CrewId != requestCrewId
                && await membershipRepository.IsUserInCrewAsync(
                    request.MiddlemanId.Value,
                    membership.CrewId,
                    cancellationToken);
            if (!middlemanInRequestCrew && !middlemanInGiverCrew)
            {
                return new EmergencyRequestOperationResponse { Success = false, Message = "Middleman is not in an accessible crew." };
            }
        }

        // Cap against remaining need only — burn-down happens on confirmation via ApplyDirectGift.
        var remaining = EmergencyRequestAccounting.GetAmountRemainingToReceive(emergencyRequest);
        var applyAmount = Math.Min(request.Amount, remaining);
        var overflowAmount = request.Amount - applyAmount;

        Gift? emergencyGift = null;
        if (applyAmount > 0m)
        {
            var seasonCycleId = await reconciliationService.GetFirstOpenEmergencySegmentIdAsync(
                emergencyRequest,
                cancellationToken);
            // Pending like other reception gifts: recipient confirms, then cycles / AmountReceived update.
            var countsTowardReception = !request.MiddlemanId.HasValue;
            emergencyGift = CreateEmergencyGift(
                requestCrewId,
                giverId,
                emergencyRequest.RequesterUserId,
                applyAmount,
                request.PaymentPlatformId,
                request.MiddlemanId,
                emergencyRequest.Id,
                seasonCycleId,
                countsTowardReception);

            await giftRepository.AddAsync(emergencyGift, cancellationToken);
            await emergencyRequestRepository.AddGiftResponseAsync(new EmergencyGiftResponse
            {
                EmergencyRequest = emergencyRequest,
                GiverUserId = giverId,
                Gift = emergencyGift,
                Amount = applyAmount,
                CreatedAt = DateTime.UtcNow
            }, cancellationToken);
        }

        if (overflowAmount > 0m)
        {
            var overflowGift = CreateUncategorizedGift(
                requestCrewId,
                giverId,
                emergencyRequest.RequesterUserId,
                overflowAmount,
                request.PaymentPlatformId);
            await giftRepository.AddAsync(overflowGift, cancellationToken);
        }

        if (applyAmount > 0m)
        {
            await mutualAidService.RecordEmergencySacrificeAsync(membership.CrewId, giverId, cancellationToken);
        }
        else
        {
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        if (applyAmount > 0m || overflowAmount > 0m)
        {
            await mutualAidService.OnCrewContributionsChangedAsync(requestCrewId, cancellationToken);
            if (membership.CrewId != requestCrewId)
            {
                await mutualAidService.OnCrewContributionsChangedAsync(membership.CrewId, cancellationToken);
            }
        }

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = overflowAmount > 0m
                ? applyAmount > 0m
                    ? "Emergency gift recorded; excess amount logged as an uncategorized gift. Awaiting confirmation."
                    : "Amount exceeds remaining emergency need; logged as an uncategorized gift."
                : "Emergency gift recorded; awaiting confirmation.",
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
        int? seasonCycleId,
        bool countsTowardReception) =>
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
            CustomGiftCategory = CustomGiftCategory.Emergency,
            CountsTowardReception = countsTowardReception,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Pending,
            EmergencyRequestId = emergencyRequestId,
            SeasonCycleId = seasonCycleId,
            ReceptionApplied = false,
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
            CustomGiftCategory = CustomGiftCategory.Other,
            CountsTowardReception = false,
            CountsTowardContribution = true,
            VerificationStatus = GiftVerificationStatus.Verified,
            ReceptionApplied = true,
            CreatedAt = DateTime.UtcNow
        };
}
