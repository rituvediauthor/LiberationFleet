namespace LiberationFleet.Server.Application.Features.Auth.Contracts;

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public UserDto? User { get; set; }

    /// <summary>True when password was accepted but an email OTP is required before issuing a JWT.</summary>
    public bool RequiresMfa { get; set; }

    /// <summary>Opaque token for POST /api/auth/verify-mfa and /api/auth/resend-mfa.</summary>
    public string? MfaChallengeToken { get; set; }
}

public class UserDto
{
    public int Id { get; set; }
    public string Username { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
}

public class PasswordResetResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
}

public class ValidateResetTokenResponse
{
    public bool IsValid { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Email { get; set; }
}

public class VerifyMfaLoginRequest
{
    public string MfaChallengeToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
}

public class ResendMfaLoginRequest
{
    public string MfaChallengeToken { get; set; } = string.Empty;
}
