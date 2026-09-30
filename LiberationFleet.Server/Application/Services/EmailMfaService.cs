using System.Security.Cryptography;
using System.Text;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Domain.Enums;
using Microsoft.Extensions.Options;

namespace LiberationFleet.Server.Application.Services;

public interface IEmailMfaService
{
    Task<(bool Success, string Message, string? ChallengeToken)> CreateAndSendAsync(
        User user,
        EmailMfaPurpose purpose,
        string? deviceId,
        string? deviceName,
        string? userAgent,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, string? ChallengeToken)> ResendAsync(
        string challengeToken,
        CancellationToken cancellationToken = default);

    Task<(bool Success, string Message, EmailMfaChallenge? Challenge)> VerifyAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken = default);
}

public class EmailMfaService(
    IEmailMfaChallengeRepository challengeRepository,
    IUnitOfWork unitOfWork,
    IEmailSender emailSender,
    IOptions<EmailMfaOptions> mfaOptions,
    ILogger<EmailMfaService> logger) : IEmailMfaService
{
    public async Task<(bool Success, string Message, string? ChallengeToken)> CreateAndSendAsync(
        User user,
        EmailMfaPurpose purpose,
        string? deviceId,
        string? deviceName,
        string? userAgent,
        CancellationToken cancellationToken = default)
    {
        await challengeRepository.InvalidateOpenAsync(user.Id, purpose, cancellationToken);

        var opts = mfaOptions.Value;
        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        var challengeToken = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var now = DateTime.UtcNow;

        var challenge = new EmailMfaChallenge
        {
            UserId = user.Id,
            ChallengeToken = challengeToken,
            CodeHash = HashCode(code, challengeToken),
            Purpose = purpose,
            CreatedAt = now,
            ExpiresAt = now.AddMinutes(Math.Max(1, opts.CodeTtlMinutes)),
            LastSentAt = now,
            AttemptCount = 0,
            DeviceId = string.IsNullOrWhiteSpace(deviceId) ? null : deviceId.Trim(),
            DeviceName = string.IsNullOrWhiteSpace(deviceName) ? null : deviceName.Trim(),
            UserAgent = string.IsNullOrWhiteSpace(userAgent) ? null : userAgent.Trim()
        };

        await challengeRepository.AddAsync(challenge, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await SendCodeEmailAsync(user.Email, code, purpose, opts.CodeTtlMinutes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to send MFA email to {Email} for purpose {Purpose}", user.Email, purpose);
            return (false, "Could not send the verification email. Try again in a moment.", null);
        }

        return (true, BuildSentMessage(purpose, user.Email), challengeToken);
    }

    public async Task<(bool Success, string Message, string? ChallengeToken)> ResendAsync(
        string challengeToken,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeToken))
        {
            return (false, "Invalid verification session.", null);
        }

        var challenge = await challengeRepository.GetActiveByTokenAsync(challengeToken.Trim(), cancellationToken);
        if (challenge is null)
        {
            return (false, "Verification session expired. Sign in again.", null);
        }

        var opts = mfaOptions.Value;
        var cooldown = TimeSpan.FromSeconds(Math.Max(15, opts.ResendCooldownSeconds));
        var wait = cooldown - (DateTime.UtcNow - challenge.LastSentAt);
        if (wait > TimeSpan.Zero)
        {
            return (false, $"Wait {Math.Ceiling(wait.TotalSeconds)} seconds before requesting another code.", challenge.ChallengeToken);
        }

        var code = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6");
        challenge.CodeHash = HashCode(code, challenge.ChallengeToken);
        challenge.LastSentAt = DateTime.UtcNow;
        challenge.ExpiresAt = DateTime.UtcNow.AddMinutes(Math.Max(1, opts.CodeTtlMinutes));
        challenge.AttemptCount = 0;

        await challengeRepository.UpdateAsync(challenge, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        try
        {
            await SendCodeEmailAsync(challenge.User.Email, code, challenge.Purpose, opts.CodeTtlMinutes, cancellationToken);
        }
        catch (Exception ex)
        {
            logger.LogError(ex, "Failed to resend MFA email for challenge {ChallengeId}", challenge.Id);
            return (false, "Could not send the verification email. Try again in a moment.", challenge.ChallengeToken);
        }

        return (true, BuildSentMessage(challenge.Purpose, challenge.User.Email), challenge.ChallengeToken);
    }

    public async Task<(bool Success, string Message, EmailMfaChallenge? Challenge)> VerifyAsync(
        string challengeToken,
        string code,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(challengeToken) || string.IsNullOrWhiteSpace(code))
        {
            return (false, "Enter the verification code from your email.", null);
        }

        var challenge = await challengeRepository.GetActiveByTokenAsync(challengeToken.Trim(), cancellationToken);
        if (challenge is null)
        {
            return (false, "Verification session expired. Sign in again.", null);
        }

        var opts = mfaOptions.Value;
        if (challenge.AttemptCount >= Math.Max(1, opts.MaxAttempts))
        {
            challenge.ConsumedAt = DateTime.UtcNow;
            await challengeRepository.UpdateAsync(challenge, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);
            return (false, "Too many incorrect codes. Sign in again to get a new code.", null);
        }

        var normalized = code.Trim().Replace(" ", "", StringComparison.Ordinal);
        var expected = HashCode(normalized, challenge.ChallengeToken);
        if (!CryptographicOperations.FixedTimeEquals(
                Encoding.UTF8.GetBytes(expected),
                Encoding.UTF8.GetBytes(challenge.CodeHash)))
        {
            challenge.AttemptCount += 1;
            await challengeRepository.UpdateAsync(challenge, cancellationToken);
            await unitOfWork.SaveChangesAsync(cancellationToken);

            var remaining = Math.Max(0, opts.MaxAttempts - challenge.AttemptCount);
            return (false, remaining > 0
                ? $"Incorrect code. {remaining} attempt{(remaining == 1 ? "" : "s")} left."
                : "Too many incorrect codes. Sign in again to get a new code.", null);
        }

        challenge.ConsumedAt = DateTime.UtcNow;
        await challengeRepository.UpdateAsync(challenge, cancellationToken);
        await unitOfWork.SaveChangesAsync(cancellationToken);

        return (true, "Verified.", challenge);
    }

    private async Task SendCodeEmailAsync(
        string to,
        string code,
        EmailMfaPurpose purpose,
        int ttlMinutes,
        CancellationToken cancellationToken)
    {
        var subject = purpose switch
        {
            EmailMfaPurpose.Enable => "Confirm email MFA for Liberation Fleet",
            EmailMfaPurpose.Disable => "Confirm turning off MFA for Liberation Fleet",
            _ => "Your Liberation Fleet sign-in code"
        };

        var action = purpose switch
        {
            EmailMfaPurpose.Enable => "turning on email two-factor authentication",
            EmailMfaPurpose.Disable => "turning off email two-factor authentication",
            _ => "signing in"
        };

        var body =
            $"Your Liberation Fleet verification code is: {code}\n\n" +
            $"Use this code for {action}. It expires in {ttlMinutes} minutes.\n\n" +
            "If you did not request this, you can ignore this message and keep your account secure.";

        await emailSender.SendAsync(to, subject, body, cancellationToken);
    }

    private static string BuildSentMessage(EmailMfaPurpose purpose, string email)
    {
        var masked = MaskEmail(email);
        return purpose switch
        {
            EmailMfaPurpose.Enable => $"We sent a confirmation code to {masked}.",
            EmailMfaPurpose.Disable => $"We sent a confirmation code to {masked} to turn MFA off.",
            _ => $"We sent a sign-in code to {masked}."
        };
    }

    private static string MaskEmail(string email)
    {
        var at = email.IndexOf('@');
        if (at <= 1)
        {
            return email;
        }

        return $"{email[0]}***{email[at..]}";
    }

    private static string HashCode(string code, string challengeToken)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"{challengeToken}:{code}"));
        return Convert.ToHexString(bytes);
    }
}
