using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.AccountRecovery.Contracts;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.AccountRecovery.Commands.CompleteAccountRecovery;

public record CompleteAccountRecoveryCommand(CompleteAccountRecoveryRequest Request)
    : IRequest<AccountRecoveryCompleteResponse>;

public class CompleteAccountRecoveryCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    ICrewMembershipRepository membershipRepository,
    IPasswordHasher passwordHasher,
    IEmailMfaService emailMfaService,
    AccountRecoveryMergeService mergeService,
    ITokenService tokenService,
    IUnitOfWork unitOfWork) : IRequestHandler<CompleteAccountRecoveryCommand, AccountRecoveryCompleteResponse>
{
    public async Task<AccountRecoveryCompleteResponse> Handle(
        CompleteAccountRecoveryCommand request,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var newUserId = currentUser.UserId.Value;
        var oldUserId = request.Request.OldUserId;
        var password = request.Request.Password?.Trim() ?? string.Empty;
        var code = request.Request.Code?.Trim() ?? string.Empty;
        var challengeToken = request.Request.ChallengeToken?.Trim() ?? string.Empty;

        if (oldUserId <= 0 || oldUserId == newUserId)
        {
            return Fail("Select a different crewmate account to recover.");
        }

        if (string.IsNullOrEmpty(password))
        {
            return Fail("Enter the password for the old account.");
        }

        if (string.IsNullOrEmpty(code) || string.IsNullOrEmpty(challengeToken))
        {
            return Fail("Enter the recovery code from the old account's email.");
        }

        var validation = await AccountRecoveryAccess.ValidateSameCrewAsync(
            membershipRepository,
            userRepository,
            newUserId,
            oldUserId,
            cancellationToken);
        if (!validation.Success || validation.OldUser is null)
        {
            return Fail(validation.Message);
        }

        if (!passwordHasher.Verify(password, validation.OldUser.PasswordHash))
        {
            return Fail("The password for that account is incorrect.");
        }

        var verified = await emailMfaService.VerifyAsync(challengeToken, code, cancellationToken);
        if (!verified.Success || verified.Challenge is null)
        {
            return Fail(verified.Message);
        }

        if (verified.Challenge.Purpose != EmailMfaPurpose.AccountRecovery
            || verified.Challenge.UserId != oldUserId)
        {
            return Fail("Invalid recovery session. Start recovery again.");
        }

        var absorbedUsername = validation.NewUser?.Username ?? string.Empty;
        var recoveredUsername = validation.OldUser.Username;

        var merge = await mergeService.MergeAsync(
            validation.CrewId,
            newUserId,
            oldUserId,
            cancellationToken);
        if (!merge.Success)
        {
            return Fail(merge.Message);
        }

        var oldUser = await userRepository.GetByIdWithProfileAsync(oldUserId, cancellationToken);
        if (oldUser is null || !oldUser.IsActive)
        {
            return Fail("Recovery completed but the recovered account could not be signed in.");
        }

        oldUser.LastLoginAt = DateTime.UtcNow;
        oldUser.FailedLoginAttempts = 0;
        oldUser.LastFailedLoginAt = null;
        SecurityStampHelper.EnsureStamp(oldUser);
        await userRepository.UpdateAsync(oldUser, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new AccountRecoveryCompleteResponse
        {
            Success = true,
            Message = merge.Message,
            Token = tokenService.GenerateJwtToken(oldUser),
            RecoveredUsername = recoveredUsername,
            AbsorbedUsername = absorbedUsername,
            User = new UserDto
            {
                Id = oldUser.Id,
                Username = oldUser.Username,
                Email = oldUser.Email
            }
        };
    }

    private static AccountRecoveryCompleteResponse Fail(string message) =>
        new() { Success = false, Message = message };
}
