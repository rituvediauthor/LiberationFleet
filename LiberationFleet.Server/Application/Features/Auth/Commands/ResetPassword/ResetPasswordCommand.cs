using LiberationFleet.Server.Application.Features.Auth.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommand : IRequest<PasswordResetResponse>
{
    public string Token { get; set; } = string.Empty;
    public string NewPassword { get; set; } = string.Empty;
    public string ConfirmPassword { get; set; } = string.Empty;
}
