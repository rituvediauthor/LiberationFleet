using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Fleets;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.DeleteAccount;

public record DeleteAccountCommand(DeleteAccountRequest Request) : IRequest<SecurityOperationResponse>;

public class DeleteAccountCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    ICrewMembershipRepository membershipRepository,
    IFleetRepository fleetRepository,
    IMutualAidService mutualAidService,
    LibraryMemberCleanupService libraryMemberCleanupService,
    EmptyCrewCleanupService emptyCrewCleanupService,
    FleetMembershipService fleetMembershipService,
    ContentTenureService contentTenureService,
    IUnitOfWork unitOfWork) : IRequestHandler<DeleteAccountCommand, SecurityOperationResponse>
{
    public async Task<SecurityOperationResponse> Handle(DeleteAccountCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new SecurityOperationResponse { Success = false, Message = "Unauthorized." };
        }

        var password = request.Request.CurrentPassword?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(password))
        {
            return new SecurityOperationResponse { Success = false, Message = "Current password is required." };
        }

        var userId = currentUser.UserId.Value;
        var user = await userRepository.GetByIdWithProfileAsync(userId, cancellationToken);
        if (user is null)
        {
            return new SecurityOperationResponse { Success = false, Message = "User not found." };
        }

        if (!passwordHasher.Verify(password, user.PasswordHash))
        {
            return new SecurityOperationResponse { Success = false, Message = "Current password is incorrect." };
        }

        var membership = await membershipRepository.GetActiveMembershipAsync(userId, cancellationToken);
        if (membership is not null)
        {
            var crewId = membership.CrewId;
            await mutualAidService.RemoveMemberFromSeasonAsync(crewId, userId, cancellationToken);
            await libraryMemberCleanupService.CleanupForDepartingMemberAsync(crewId, userId, cancellationToken);
            await contentTenureService.OnLeftCrewAsync(userId, crewId, cancellationToken);
            membershipRepository.Remove(membership);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            await emptyCrewCleanupService.TryCleanupIfNoActiveMembersAsync(crewId, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
        }

        var fleetMembership = await fleetRepository.GetFleetMembershipForUserAsync(userId, cancellationToken);
        if (fleetMembership is not null)
        {
            await contentTenureService.PauseFleetAsync(userId, fleetMembership.FleetId, cancellationToken);
            await fleetMembershipService.ClearFleetMembershipAsync(userId, fleetMembership.FleetId, cancellationToken);
        }

        user.Username = $"deleted{userId}";
        user.Email = $"deleted-{userId}@deleted.invalid";
        user.PasswordHash = passwordHasher.Hash(Guid.NewGuid().ToString("N") + Guid.NewGuid().ToString("N"));
        user.IsActive = false;
        user.AvatarResourceId = null;
        user.SettingsLockPasswordHash = null;
        user.LockSettingsWithPassword = false;
        user.TwoFactorEnabled = false;
        user.NeedsSurvivalAid = false;
        user.InNeedOfAid = false;
        user.IdentityGroups = null;
        foreach (var platform in user.PaymentPlatforms.ToList())
        {
            user.PaymentPlatforms.Remove(platform);
        }

        SecurityStampHelper.Bump(user);
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new SecurityOperationResponse
        {
            Success = true,
            Message = "Your account has been deleted."
        };
    }
}
