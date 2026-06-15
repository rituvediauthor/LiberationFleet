namespace LiberationFleet.Server.Application.Features.Auth.Contracts;

public class LoginResponse
{
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public string? Token { get; set; }
    public UserDto? User { get; set; }
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
