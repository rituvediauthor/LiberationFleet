using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews.Contracts;
using LiberationFleet.Server.Application.Features.Library;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crews.Commands.LeaveCrew;

public record LeaveCrewCommand : IRequest<CrewOperationResponse>;

public class LeaveCrewCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    IUnitOfWork unitOfWork) : IRequestHandler<LeaveCrewCommand, CrewOperationResponse>
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
            return new CrewOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var crewId = membership.CrewId;
        var userId = currentUser.UserId.Value;
        await libraryMemberCleanupService.CleanupForDepartingMemberAsync(crewId, userId, cancellationToken);
        membershipRepository.Remove(membership);
        await unitOfWork.SaveChangesAsync(cancellationToken);
        await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(crewId, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewOperationResponse
        {
            Success = true,
            Message = "You have left the crew."
        };
    }
}
