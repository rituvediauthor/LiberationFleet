using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crewmates.Contracts;
using LiberationFleet.Server.Application.Features.Crews;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Crewmates.Commands.MergePlaceholderIntoUser;

public record MergePlaceholderIntoUserCommand(
    int PlaceholderUserId,
    int TargetUserId) : IRequest<CrewmateKickResponse>;

public class MergePlaceholderIntoUserCommandHandler(
    ICurrentUserService currentUser,
    ICrewMembershipRepository membershipRepository,
    IUserRepository userRepository,
    IProposalRepository proposalRepository,
    PlaceholderCrewmateService placeholderCrewmateService,
    IUnitOfWork unitOfWork) : IRequestHandler<MergePlaceholderIntoUserCommand, CrewmateKickResponse>
{
    public async Task<CrewmateKickResponse> Handle(
        MergePlaceholderIntoUserCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new CrewmateKickResponse { Success = false, Message = "Unauthorized." };
        }

        var viewerId = currentUser.UserId.Value;
        var viewerMembership = await membershipRepository.GetActiveMembershipAsync(viewerId, cancellationToken);
        if (viewerMembership is null)
        {
            return new CrewmateKickResponse { Success = false, Message = "You are not in a crew." };
        }

        if (!CrewRoleAuthorizationService.CanManagePlaceholders(viewerMembership))
        {
            return new CrewmateKickResponse
            {
                Success = false,
                Message = "Only organizers and accountants can merge a placeholder into a real account."
            };
        }

        if (request.PlaceholderUserId <= 0
            || request.TargetUserId <= 0
            || request.PlaceholderUserId == request.TargetUserId)
        {
            return new CrewmateKickResponse { Success = false, Message = "Select a real crewmate to merge into." };
        }

        var crewId = viewerMembership.CrewId;
        var placeholderUser = await userRepository.GetByIdWithProfileAsync(
            request.PlaceholderUserId,
            cancellationToken);
        if (placeholderUser is null || !placeholderUser.IsUnclaimedPlaceholder)
        {
            return new CrewmateKickResponse
            {
                Success = false,
                Message = "That person is not an unclaimed placeholder."
            };
        }

        var placeholderMembership = await membershipRepository.GetMembershipAsync(
            request.PlaceholderUserId,
            crewId,
            cancellationToken);
        if (placeholderMembership is null
            || placeholderMembership.IsBanned
            || !placeholderMembership.IsPlaceholderMember)
        {
            return new CrewmateKickResponse { Success = false, Message = "Placeholder not found in your crew." };
        }

        var targetUser = await userRepository.GetByIdWithProfileAsync(request.TargetUserId, cancellationToken);
        if (targetUser is null || targetUser.IsUnclaimedPlaceholder || !targetUser.IsActive)
        {
            return new CrewmateKickResponse
            {
                Success = false,
                Message = "Choose an active, non-placeholder crewmate as the merge target."
            };
        }

        var targetMembership = await membershipRepository.GetMembershipAsync(
            request.TargetUserId,
            crewId,
            cancellationToken);
        if (targetMembership is null
            || targetMembership.IsBanned
            || targetMembership.LeftAt is not null
            || targetMembership.IsPlaceholderMember)
        {
            return new CrewmateKickResponse
            {
                Success = false,
                Message = "Choose an active, non-placeholder crewmate as the merge target."
            };
        }

        var pendingClaim = await proposalRepository.GetPendingClaimPlaceholderIdentityForPlaceholderAsync(
            crewId,
            request.PlaceholderUserId,
            cancellationToken);
        if (pendingClaim is not null)
        {
            return new CrewmateKickResponse
            {
                Success = false,
                Message = "An identity-claim proposal is already pending for this placeholder. Resolve or wait for that vote first.",
                ProposalId = pendingClaim.ProposalId
            };
        }

        var placeholderName = UserDisplay.Name(placeholderUser);
        var targetName = UserDisplay.Name(targetUser);

        await placeholderCrewmateService.MergePlaceholderIntoClaimantAsync(
            crewId,
            request.PlaceholderUserId,
            request.TargetUserId,
            cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new CrewmateKickResponse
        {
            Success = true,
            Message =
                $"Merged placeholder {placeholderName} into {targetName}. " +
                "Reception history, payment handles they were missing, and default mutual-aid fields were updated."
        };
    }
}
