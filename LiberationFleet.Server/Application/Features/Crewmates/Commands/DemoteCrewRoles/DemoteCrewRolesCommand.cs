using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.DemoteCrewRoles;

public record DemoteCrewRolesCommand(int TargetUserId, IReadOnlyList<string> Roles) : IRequest<CrewRoleChangeResponse>;

public class DemoteCrewRolesCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    CrewRoleProposalService roleProposalService,
    IMutualAidService mutualAidService,
    IUnitOfWork unitOfWork) : IRequestHandler<DemoteCrewRolesCommand, CrewRoleChangeResponse>
{
    public async Task<CrewRoleChangeResponse> Handle(DemoteCrewRolesCommand request, CancellationToken cancellationToken)
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
        if (roles.Count == 0)
        {
            return new CrewRoleChangeResponse { Success = false, Message = "Select at least one role." };
        }

        // Any crewmate can relinquish their own elected roles immediately — no proposal required.
        if (viewerId == request.TargetUserId)
        {
            roles = roles.Where(role => CrewRoleMapper.HasRole(viewerMembership, role)).ToList();
            if (roles.Count == 0)
            {
                return new CrewRoleChangeResponse
                {
                    Success = false,
                    Message = "You do not hold the selected roles."
                };
            }

            CrewRoleMapper.ApplyRoles(viewerMembership, roles, assign: false);
            if (roles.Contains(CrewRole.HonoraryMember))
            {
                viewerMembership.CurrentPriorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
                    viewerMembership.UserId,
                    viewerMembership.CrewId,
                    cancellationToken);
            }

            await unitOfWork.SaveChangesAsync(cancellationToken);

            var labels = string.Join(", ", roles.Select(CrewRoleMapper.GetDisplayName));
            return new CrewRoleChangeResponse
            {
                Success = true,
                Message = roles.Count == 1
                    ? $"{labels} removed from your roles."
                    : $"Removed from your roles: {labels}."
            };
        }

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
            if (!targetMembership.IsHonoraryMember)
            {
                return new CrewRoleChangeResponse
                {
                    Success = false,
                    Message = "This placeholder is already marked as a non-member."
                };
            }

            targetMembership.IsHonoraryMember = false;
            targetMembership.CurrentPriorityScore = await mutualAidService.GetPriorityScoreForUserAsync(
                targetMembership.UserId,
                targetMembership.CrewId,
                cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            return new CrewRoleChangeResponse
            {
                Success = true,
                Message = "Placeholder marked as a non-member."
            };
        }

        var result = await roleProposalService.CreateDemotionAsync(
            viewerMembership.CrewId,
            viewerId,
            request.TargetUserId,
            roles,
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
