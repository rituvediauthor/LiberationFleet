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

        if (emergencyRequest.Status == EmergencyRequestStatus.Cancelled)
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

        var reconciliation = await reconciliationService.ApplyDirectGiftAsync(
            emergencyRequest,
            request.Amount,
            cancellationToken);

        Gift? emergencyGift = null;
        if (reconciliation.AmountAppliedToNeed > 0m)
        {
            int? seasonCycleId = null;
            var requestCrew = await mutualAidRepository.GetCrewAsync(requestCrewId, cancellationToken);
            if (requestCrew?.CurrentSeasonStartDate is not null)
            {
                var cycles = await mutualAidRepository.GetSeasonCyclesAsync(
                    requestCrewId,
                    requestCrew.CurrentSeasonStartDate.Value,
                    cancellationToken);
                seasonCycleId = cycles
                    .Where(c => c.EmergencyRequestId == emergencyRequest.Id && !c.CycleCompleted)
                    .OrderBy(c => c.ReceptionOrderPosition)
                    .FirstOrDefault()?.Id;
            }

            emergencyGift = CreateEmergencyGift(
                requestCrewId,
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
                requestCrewId,
                giverId,
                emergencyRequest.RequesterUserId,
                reconciliation.OverflowAmount,
                request.PaymentPlatformId);
            await giftRepository.AddAsync(overflowGift, cancellationToken);
        }

        // Sacrifice counter lives on the giver's home-crew membership.
        await mutualAidService.RecordEmergencySacrificeAsync(membership.CrewId, giverId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        if (emergencyGift?.CountsTowardReception == true)
        {
            await mutualAidService.ApplyGiftReceptionAsync(emergencyGift, cancellationToken);
        }

        if (reconciliation.AmountAppliedToNeed > 0m)
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
