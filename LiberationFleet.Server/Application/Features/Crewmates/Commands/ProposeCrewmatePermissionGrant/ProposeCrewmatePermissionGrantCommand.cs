using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Domain.Enums;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.ProposeCrewmatePermissionGrant;

public record ProposeCrewmatePermissionGrantCommand(int TargetUserId, CrewmatePermissionGrantType GrantType)
    : IRequest<CrewRoleChangeResponse>;

public class ProposeCrewmatePermissionGrantCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    CrewmatePermissionProposalService permissionProposalService,
    IUnitOfWork unitOfWork) : IRequestHandler<ProposeCrewmatePermissionGrantCommand, CrewRoleChangeResponse>
{
    public async Task<CrewRoleChangeResponse> Handle(
        ProposeCrewmatePermissionGrantCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(currentUser.UserId.Value, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "You are not in a crew." };
        }

        if (request.TargetUserId == currentUser.UserId.Value)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "You cannot propose permission grants for yourself." };
        }

        var result = request.GrantType switch
        {
            CrewmatePermissionGrantType.AttachFiles => await permissionProposalService.CreateAttachFilesGrantAsync(
                viewerMembership.CrewId,
                currentUser.UserId.Value,
                request.TargetUserId,
                cancellationToken),
            CrewmatePermissionGrantType.CreateProposals => await permissionProposalService.CreateCreateProposalsGrantAsync(
                viewerMembership.CrewId,
                currentUser.UserId.Value,
                request.TargetUserId,
                cancellationToken),
            _ => CrewmatePermissionProposalResult.Failed("Unsupported permission grant type.")
        };

        if (!result.Success)
        {
            return new CrewRoleChangeResponse
            {
                Success = false,
                Message = result.Message,
                ProposalId = result.ProposalId
            };
        }

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewRoleChangeResponse
        {
            Success = true,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
