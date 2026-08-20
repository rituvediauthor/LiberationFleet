using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.EmergencyRequests.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.EmergencyRequests.Queries.GetEmergencyRequestDetail;

public record GetEmergencyRequestDetailQuery(int RequestId) : IRequest<EmergencyRequestDetailResponse>;

public class GetEmergencyRequestDetailQueryHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IEmergencyRequestRepository emergencyRequestRepository,
    IFleetRepository fleetRepository,
    IMutualAidRepository mutualAidRepository,
    IMutualAidService mutualAidService,
    EmergencySplitService emergencySplitService) : IRequestHandler<GetEmergencyRequestDetailQuery, EmergencyRequestDetailResponse>
{
    public async Task<EmergencyRequestDetailResponse> Handle(
        GetEmergencyRequestDetailQuery request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmergencyRequestDetailResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (membership is null)
        {
            return new EmergencyRequestDetailResponse { Success = false, Message = "You are not in a crew." };
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
            return new EmergencyRequestDetailResponse { Success = false, Message = accessError ?? "Emergency request not found." };
        }

        var requestCrewId = emergencyRequest.CrewId;
        var viewer = await membershipRepository.GetActiveMembersByCrewIdAsync(membership.CrewId, cancellationToken);
        var viewerMember = viewer.FirstOrDefault(m => m.UserId == viewerId);
        var viewerPlatformIds = viewerMember?.User.PaymentPlatforms
            .Select(p => p.CrewPaymentPlatformId)
            .ToHashSet() ?? [];

        var requesterPlatforms = emergencyRequest.RequesterUser.PaymentPlatforms
            .OrderByDescending(p => p.IsPreferred)
            .ThenBy(p => p.Id)
            .ToList();

        var commonPlatforms = requesterPlatforms
            .Where(p => viewerPlatformIds.Contains(p.CrewPaymentPlatformId))
            .Select(p => new EmergencyPlatformDto
            {
                PlatformId = p.CrewPaymentPlatformId,
                PlatformName = p.CrewPaymentPlatform?.Name ?? string.Empty,
                Handle = p.Handle,
                IsPreferred = p.IsPreferred,
                IsSharedWithViewer = true
            })
            .ToList();

        if (commonPlatforms.Count == 0)
        {
            var preferred = requesterPlatforms.FirstOrDefault(p => p.IsPreferred) ?? requesterPlatforms.FirstOrDefault();
            if (preferred is not null)
            {
                commonPlatforms.Add(new EmergencyPlatformDto
                {
                    PlatformId = preferred.CrewPaymentPlatformId,
                    PlatformName = preferred.CrewPaymentPlatform?.Name ?? string.Empty,
                    Handle = preferred.Handle,
                    IsPreferred = preferred.IsPreferred,
                    IsSharedWithViewer = false
                });
            }
        }

        // Middlemen from the request crew (and the viewer's crew when cross-crew).
        var allMembers = (await mutualAidRepository.GetActiveMembersWithUsersAsync(requestCrewId, cancellationToken)).ToList();
        if (membership.CrewId != requestCrewId)
        {
            var viewerCrewMembers = await mutualAidRepository.GetActiveMembersWithUsersAsync(
                membership.CrewId,
                cancellationToken);
            allMembers.AddRange(viewerCrewMembers.Where(m => allMembers.All(existing => existing.UserId != m.UserId)));
        }

        var memberPlatforms = allMembers.Select(CrewPaymentPlatformService.MapCrewMemberPlatforms).ToList();
        var middlemanIds = mutualAidService.FindMiddlemen(
            viewerId,
            emergencyRequest.RequesterUserId,
            memberPlatforms);
        var middlemanOptions = memberPlatforms
            .Where(m => middlemanIds.Contains(m.UserId))
            .Select(m => new EmergencyMiddlemanOptionDto
            {
                UserId = m.UserId,
                Username = m.Username,
                CommonPlatformIds = m.PlatformIds
                    .Where(id => viewerPlatformIds.Contains(id))
                    .Intersect(emergencyRequest.RequesterUser.PaymentPlatforms.Select(p => p.CrewPaymentPlatformId))
                    .ToList()
            })
            .ToList();

        var splitEligibility = await emergencySplitService.GetViewerSplitEligibilityAsync(
            emergencyRequest,
            viewerId,
            cancellationToken);

        var amounts = EmergencyRequestDtoMapper.MapAmounts(emergencyRequest);
        var requestCrew = await mutualAidRepository.GetCrewAsync(requestCrewId, cancellationToken);

        return new EmergencyRequestDetailResponse
        {
            Success = true,
            Message = "Emergency request loaded.",
            Request = new EmergencyRequestDetailDto
            {
                Id = emergencyRequest.Id,
                CrewId = requestCrewId,
                CrewName = requestCrew?.Name ?? string.Empty,
                RequesterUserId = emergencyRequest.RequesterUserId,
                RequesterUsername = emergencyRequest.RequesterUser.Username,
                Purpose = emergencyRequest.Purpose,
                AmountNeeded = emergencyRequest.AmountNeeded,
                AmountFulfilled = amounts.AmountReceived,
                AmountReceived = amounts.AmountReceived,
                AmountSplitCommitted = amounts.AmountSplitCommitted,
                AmountUncovered = amounts.AmountUncovered,
                AmountRemaining = amounts.AmountRemaining,
                Status = emergencyRequest.Status.ToString(),
                CreatedAt = emergencyRequest.CreatedAt,
                CommonPlatforms = commonPlatforms,
                MiddlemanOptions = middlemanOptions,
                IsSelfRequest = emergencyRequest.RequesterUserId == viewerId,
                CanViewerSplitCycle = splitEligibility.CanSplit,
                ViewerSplitMaxAmount = splitEligibility.MaxSplitAmount,
                SplitAvailabilityMessage = splitEligibility.Message
            }
        };
    }
}
