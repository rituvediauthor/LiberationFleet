using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Security;
using LiberationFleet.Server.Application.Features.Security.Contracts;
using LiberationFleet.Server.Application.Services;
using LiberationFleet.Server.Domain.Enums;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Security.Commands.EmailMfa;

public class BeginEmailMfaRequest
{
    public string? SettingsPassword { get; set; }
}

public class ConfirmEmailMfaRequest
{
    public string MfaChallengeToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string? SettingsPassword { get; set; }
}

public class EmailMfaChallengeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? MfaChallengeToken { get; set; }
    public SecuritySettingsDto? Settings { get; set; }
}

public record BeginEnableEmailMfaCommand(BeginEmailMfaRequest Request) : IRequest<EmailMfaChallengeResponse>;
public record ConfirmEnableEmailMfaCommand(ConfirmEmailMfaRequest Request) : IRequest<EmailMfaChallengeResponse>;
public record BeginDisableEmailMfaCommand(BeginEmailMfaRequest Request) : IRequest<EmailMfaChallengeResponse>;
public record ConfirmDisableEmailMfaCommand(ConfirmEmailMfaRequest Request) : IRequest<EmailMfaChallengeResponse>;
public record ResendEmailMfaSettingsCommand(ResendMfaSettingsRequest Request) : IRequest<EmailMfaChallengeResponse>;

public class ResendMfaSettingsRequest
{
    public string MfaChallengeToken { get; set; } = string.Empty;
}

public class BeginEnableEmailMfaCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailMfaService emailMfaService) : IRequestHandler<BeginEnableEmailMfaCommand, EmailMfaChallengeResponse>
{
    public async Task<EmailMfaChallengeResponse> Handle(BeginEnableEmailMfaCommand request, CancellationToken cancellationToken)
    {
        var access = await LoadUserAsync(currentUser, userRepository, cancellationToken);
        if (!access.Success || access.User is null)
        {
            return Fail(access.Message);
        }

        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(
            access.User, request.Request.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return Fail(lockCheck.Message);
        }

        if (access.User.TwoFactorEnabled)
        {
            return Fail("Email MFA is already enabled.");
        }

        var sent = await emailMfaService.CreateAndSendAsync(
            access.User, EmailMfaPurpose.Enable, null, null, null, cancellationToken);
        if (!sent.Success || string.IsNullOrWhiteSpace(sent.ChallengeToken))
        {
            return Fail(sent.Message);
        }

        return new EmailMfaChallengeResponse
        {
            Success = true,
            Message = sent.Message,
            MfaChallengeToken = sent.ChallengeToken
        };
    }

    private static async Task<(bool Success, string Message, Domain.Entities.User? User)> LoadUserAsync(
        ICurrentUserService currentUser,
        IUserRepository userRepository,
        CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return (false, "Unauthorized.", null);
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        return user is null
            ? (false, "User not found.", null)
            : (true, string.Empty, user);
    }

    private static EmailMfaChallengeResponse Fail(string message) =>
        new() { Success = false, Message = message };
}

public class ConfirmEnableEmailMfaCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailMfaService emailMfaService,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmEnableEmailMfaCommand, EmailMfaChallengeResponse>
{
    public async Task<EmailMfaChallengeResponse> Handle(ConfirmEnableEmailMfaCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Fail("User not found.");
        }

        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(
            user, request.Request.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return Fail(lockCheck.Message);
        }

        var verified = await emailMfaService.VerifyAsync(
            request.Request.MfaChallengeToken, request.Request.Code, cancellationToken);
        if (!verified.Success || verified.Challenge is null)
        {
            return Fail(verified.Message);
        }

        if (verified.Challenge.Purpose != EmailMfaPurpose.Enable
            || verified.Challenge.UserId != user.Id)
        {
            return Fail("Invalid verification session.");
        }

        user.TwoFactorEnabled = true;
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EmailMfaChallengeResponse
        {
            Success = true,
            Message = "Email MFA is on. We will email a code each time you sign in.",
            Settings = MapSettings(user)
        };
    }

    private static EmailMfaChallengeResponse Fail(string message) =>
        new() { Success = false, Message = message };

    private static SecuritySettingsDto MapSettings(Domain.Entities.User user) => new()
    {
        TwoFactorEnabled = user.TwoFactorEnabled,
        MfaAvailable = true,
        MfaMethod = user.TwoFactorEnabled ? "EmailOtp" : null,
        LockSettingsWithPassword = user.LockSettingsWithPassword,
        HasSettingsLockPassword = !string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash)
    };
}

public class BeginDisableEmailMfaCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailMfaService emailMfaService) : IRequestHandler<BeginDisableEmailMfaCommand, EmailMfaChallengeResponse>
{
    public async Task<EmailMfaChallengeResponse> Handle(BeginDisableEmailMfaCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Fail("User not found.");
        }

        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(
            user, request.Request.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return Fail(lockCheck.Message);
        }

        if (!user.TwoFactorEnabled)
        {
            return Fail("Email MFA is already off.");
        }

        var sent = await emailMfaService.CreateAndSendAsync(
            user, EmailMfaPurpose.Disable, null, null, null, cancellationToken);
        if (!sent.Success || string.IsNullOrWhiteSpace(sent.ChallengeToken))
        {
            return Fail(sent.Message);
        }

        return new EmailMfaChallengeResponse
        {
            Success = true,
            Message = sent.Message,
            MfaChallengeToken = sent.ChallengeToken
        };
    }

    private static EmailMfaChallengeResponse Fail(string message) =>
        new() { Success = false, Message = message };
}

public class ConfirmDisableEmailMfaCommandHandler(
    ICurrentUserService currentUser,
    IUserRepository userRepository,
    IPasswordHasher passwordHasher,
    IEmailMfaService emailMfaService,
    IUnitOfWork unitOfWork) : IRequestHandler<ConfirmDisableEmailMfaCommand, EmailMfaChallengeResponse>
{
    public async Task<EmailMfaChallengeResponse> Handle(ConfirmDisableEmailMfaCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return Fail("Unauthorized.");
        }

        var user = await userRepository.GetByIdWithProfileAsync(currentUser.UserId.Value, cancellationToken);
        if (user is null)
        {
            return Fail("User not found.");
        }

        var lockCheck = await SettingsLockHelper.VerifySettingsPasswordAsync(
            user, request.Request.SettingsPassword, passwordHasher);
        if (!lockCheck.Allowed)
        {
            return Fail(lockCheck.Message);
        }

        var verified = await emailMfaService.VerifyAsync(
            request.Request.MfaChallengeToken, request.Request.Code, cancellationToken);
        if (!verified.Success || verified.Challenge is null)
        {
            return Fail(verified.Message);
        }

        if (verified.Challenge.Purpose != EmailMfaPurpose.Disable
            || verified.Challenge.UserId != user.Id)
        {
            return Fail("Invalid verification session.");
        }

        user.TwoFactorEnabled = false;
        await userRepository.UpdateAsync(user, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return new EmailMfaChallengeResponse
        {
            Success = true,
            Message = "Email MFA is off.",
            Settings = new SecuritySettingsDto
            {
                TwoFactorEnabled = false,
                MfaAvailable = true,
                MfaMethod = null,
                LockSettingsWithPassword = user.LockSettingsWithPassword,
                HasSettingsLockPassword = !string.IsNullOrWhiteSpace(user.SettingsLockPasswordHash)
            }
        };
    }

    private static EmailMfaChallengeResponse Fail(string message) =>
        new() { Success = false, Message = message };
}

public class ResendEmailMfaSettingsCommandHandler(
    ICurrentUserService currentUser,
    IEmailMfaService emailMfaService,
    IEmailMfaChallengeRepository challengeRepository) : IRequestHandler<ResendEmailMfaSettingsCommand, EmailMfaChallengeResponse>
{
    public async Task<EmailMfaChallengeResponse> Handle(ResendEmailMfaSettingsCommand request, CancellationToken cancellationToken)
    {
        if (!currentUser.UserId.HasValue)
        {
            return new EmailMfaChallengeResponse { Success = false, Message = "Unauthorized." };
        }

        var existing = await challengeRepository.GetActiveByTokenAsync(
            request.Request.MfaChallengeToken?.Trim() ?? string.Empty, cancellationToken);
        if (existing is null || existing.UserId != currentUser.UserId.Value
            || (existing.Purpose != EmailMfaPurpose.Enable && existing.Purpose != EmailMfaPurpose.Disable))
        {
            return new EmailMfaChallengeResponse
            {
                Success = false,
                Message = "Verification session expired. Start again."
            };
        }

        var sent = await emailMfaService.ResendAsync(request.Request.MfaChallengeToken, cancellationToken);
        return new EmailMfaChallengeResponse
        {
            Success = sent.Success,
            Message = sent.Message,
            MfaChallengeToken = sent.ChallengeToken
        };
    }
}
