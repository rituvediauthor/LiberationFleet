using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.ToggleCanAttachFiles;

public record ToggleCanAttachFilesCommand(int TargetUserId, bool CanAttachFiles) : IRequest<CrewmateOperationResponse>;

public class ToggleCanAttachFilesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUnitOfWork unitOfWork) : IRequestHandler<ToggleCanAttachFilesCommand, CrewmateOperationResponse>
{
    public async Task<CrewmateOperationResponse> Handle(ToggleCanAttachFilesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateOperationResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!CrewRoleAuthorizationService.CanToggleCanAttachFiles(viewerMembership))
        {
            return new CrewmateOperationResponse { Success = false, Message = "You do not have permission to change attachment settings." };
        }

        var targetMembership = await membershipRepository.GetMembershipAsync(
            request.TargetUserId,
            viewerMembership.CrewId,
            cancellationToken);
        if (targetMembership is null || targetMembership.IsBanned)
        {
            return new CrewmateOperationResponse { Success = false, Message = "Crewmate not found." };
        }

        targetMembership.CanAttachFiles = request.CanAttachFiles;
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateOperationResponse
        {
            Success = true,
            Message = request.CanAttachFiles
                ? "This crewmate can attach files again."
                : "This crewmate can no longer attach files."
        };
    }
}
