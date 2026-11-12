using LiberationFleet.Server.Application.Features.Auth.Contracts;

namespace LiberationFleet.Server.Application.Features.AccountRecovery.Contracts;

public class BeginAccountRecoveryRequest
{
    public int OldUserId { get; set; }
}

public class ResendAccountRecoveryRequest
{
    public string ChallengeToken { get; set; } = string.Empty;
}

public class CompleteAccountRecoveryRequest
{
    public int OldUserId { get; set; }
    public string ChallengeToken { get; set; } = string.Empty;
    public string Code { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
}

public class AccountRecoveryChallengeResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? ChallengeToken { get; set; }
    public string? MaskedEmail { get; set; }
    public string? OldUsername { get; set; }
}

public class AccountRecoveryCompleteResponse : LoginResponse
{
    /// <summary>Surviving (old) account username after recovery.</summary>
    public string? RecoveredUsername { get; set; }

    /// <summary>Username of the absorbing account before it was deleted (held the 12-word key).</summary>
    public string? AbsorbedUsername { get; set; }
}
