using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.NominateCrewRoles;

public record NominateCrewRolesCommand(
    int TargetUserId,
    IReadOnlyList<string> Roles,
    DateTime? RepresentativeTermStartUtc = null,
    DateTime? RepresentativeTermEndUtc = null) : IRequest<CrewRoleChangeResponse>;

public class NominateCrewRolesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    CrewRoleProposalService roleProposalService,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<NominateCrewRolesCommand, CrewRoleChangeResponse>
{
    public async Task<CrewRoleChangeResponse> Handle(NominateCrewRolesCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!await membershipRepository.IsUserInCrewAsync(request.TargetUserId, viewerMembership.CrewId, cancellationToken))
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Crewmate not found." };
        }

        var roles = CrewRoleMapper.ParseRoles(request.Roles);
        var targetMembership = await membershipRepository.GetMembershipAsync(
            request.TargetUserId,
            viewerMembership.CrewId,
            cancellationToken);

        // Placeholders cannot vote on proposals — organizers/accountants set membership directly.
        if (targetMembership?.IsPlaceholderMember == true
            && CrewRoleAuthorizationService.CanManagePlaceholders(viewerMembership)
            && roles.Count == 1
            && roles[0] == CrewRole.HonoraryMember)
        {
            if (targetMembership.IsHonoraryMember)
            {
                return new CrewRoleChangeResponse
                {
                    Success = false,
                    Message = "This placeholder is already marked as a financial member."
                };
            }

            targetMembership.IsHonoraryMember = true;
            targetMembership.CurrentPriorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
                targetMembership.UserId,
                targetMembership.CrewId,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new CrewRoleChangeResponse
            {
                Success = true,
                Message = "Placeholder marked as a financial member."
            };
        }

        var result = await roleProposalService.CreateNominationAsync(
            viewerMembership.CrewId,
            viewerId,
            request.TargetUserId,
            roles,
            request.RepresentativeTermStartUtc,
            request.RepresentativeTermEndUtc,
            cancellationToken);

        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewRoleChangeResponse
        {
            Success = result.Success,
            Message = result.Message,
            ProposalId = result.ProposalId
        };
    }
}
