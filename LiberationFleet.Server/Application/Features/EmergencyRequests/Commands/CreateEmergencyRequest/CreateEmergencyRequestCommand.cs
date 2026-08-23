using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Commands.CreateEmergencyRequest;

public record CreateEmergencyRequestCommand(string Purpose, decimal AmountNeeded)
    : IRequest<EmergencyRequestOperationResponse>;

public class CreateEmergencyRequestCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IUserRepository userRepository,
    EmergencySplitService emergencySplitService,
    IMutualAidService mutualAidService,
    NotificationService notificationService,
    IUnitOfWork unitOfWork) : IRequestHandler<CreateEmergencyRequestCommand, EmergencyRequestOperationResponse>
{
    public async Task<EmergencyRequestOperationResponse> Handle(
        CreateEmergencyRequestCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var purpose = request.Purpose.Trim();
        if (string.IsNullOrWhiteSpace(purpose))
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Purpose is required." };
        }

        if (request.AmountNeeded <= 0)
        {
            return new EmergencyRequestOperationResponse { Success = false, Message = "Amount needed must be greater than zero." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null || !membership.IsInSeason)
        {
            return new EmergencyRequestOperationResponse
            {
                Success = false,
                Message = "You must be in an active season to create an emergency request."
            };
        }

        await mutualAidService.EnsureNextSeasonCyclesAsync(membership.CrewId, cancellationToken);

        var requester = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        var requesterName = requester?.Username ?? "A crewmate";

        var eligibleOfferers = await emergencySplitService.CaptureEligibleOffererUserIdsAsync(
            membership.CrewId,
            currentUser.UserId.Value,
            cancellationToken);

        var emergencyRequest = new EmergencyRequest
        {
            CrewId = membership.CrewId,
            RequesterUserId = currentUser.UserId.Value,
            Purpose = purpose,
            AmountNeeded = request.AmountNeeded,
            AmountReceived = 0m,
            AmountSplitCommitted = 0m,
            Status = EmergencyRequestStatus.Open,
            CreatedAt = DateTime.UtcNow,
            SplitEligibleOffererUserIds = EmergencySplitService.FormatEligibleOffererUserIds(eligibleOfferers)
        };

        await emergencyRequestRepository.AddAsync(emergencyRequest, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        var purposePreview = purpose.Length > 80 ? purpose[..77] + "..." : purpose;
        await notificationService.NotifyCrewAsync(
            membership.CrewId,
            NotificationKind.NewEmergencyRequest,
            "Emergency request",
            $"{requesterName} needs ${request.AmountNeeded:0.##} for: {purposePreview}",
            $"/app/crew/emergency-requests/{emergencyRequest.Id}?highlightId={emergencyRequest.Id}",
            relatedEntityId: emergencyRequest.Id,
            excludeUserId: currentUser.UserId.Value,
            cancellationToken: cancellationToken);

        return new EmergencyRequestOperationResponse
        {
            Success = true,
            Message = "Emergency request submitted.",
            RequestId = emergencyRequest.Id
        };
    }
}
