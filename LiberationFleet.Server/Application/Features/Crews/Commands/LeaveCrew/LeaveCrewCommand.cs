using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Services;
using MediatR;
using Microsoft.Extensions.Logging;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.LeaveCrew;

public record LeaveCrewCommand : IRequest<CrewOperationResponse>;

public class LeaveCrewCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    FleetMembershipService fleetMembershipService,
    IMutualAidService mutualAidService,
    UserPaymentPlatformPortabilityService paymentPlatformPortability,
    IUnitOfWork unitOfWork,
    ILogger<LeaveCrewCommandHandler> logger) : IRequestHandler<LeaveCrewCommand, CrewOperationResponse>
{
    public async Task<CrewOperationResponse> Handle(LeaveCrewCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (membership is null)
        {
            // Idempotent: a prior leave may have committed while a follow-up cleanup failed.
            return new CrewOperationResponse { Success = true, Message = "You have left the crew." };
        }

        var crewId = membership.CrewId;
        var userId = currentUser.UserId.Value;
        await libraryMemberCleanupService.CleanupForDepartingMemberAsync(crewId, userId, cancellationToken);
        await mutualAidService.RemoveMemberFromSeasonAsync(crewId, userId, cancellationToken);
        await paymentPlatformPortability.DetachFromCrewAsync(userId, cancellationToken);
        // Handles crew tenure pause and keeps fleet tenure when the member stays in a fleet.
        await fleetMembershipService.RetainInFleetAsNoCrewAsync(userId, crewId, cancellationToken);
        membershipRepository.MarkLeft(membership, DateTime.UtcNow);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(crewId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }
        catch (Exception ex)
        {
            // Leave already committed; do not turn a successful leave into a client error toast.
            logger.LogWarning(ex, "Empty-crew cleanup failed after user {UserId} left crew {CrewId}", userId, crewId);
        }

        return new CrewOperationResponse
        {
            Success = true,
            Message = "You have left the crew."
        };
    }
}
