using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Library.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Library.Commands.ReportLibraryUnitLost;

public record ReportLibraryUnitLostCommand(int UnitId) : IRequest<LibraryUnitOperationResponse>;

public class ReportLibraryUnitLostCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    ILibraryRepository libraryRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ReportLibraryUnitLostCommand, LibraryUnitOperationResponse>
{
    public async Task<LibraryUnitOperationResponse> Handle(
        ReportLibraryUnitLostCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var userId = currentUser.UserId.Value;
        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is null)
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        var trackedUnit = await libraryRepository.GetTrackedUnitByIdAsync(request.UnitId, cancellationToken);
        if (trackedUnit is null || trackedUnit.Offering.CrewId != membership.CrewId)
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "Item not found." };
        }

        if (!LibraryUnitAccess.CanReportLost(trackedUnit, userId))
        {
            return new LibraryUnitOperationResponse { Success = false, Message = "This item cannot be reported lost." };
        }

        trackedUnit.IsRetired = true;
        trackedUnit.Offering.UpdatedAt = DateTime.UtcNow;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new LibraryUnitOperationResponse
        {
            Success = true,
            Message = "Item reported lost and removed from listings.",
            UnitId = trackedUnit.Id
        };
    }
}
