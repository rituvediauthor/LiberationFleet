using LiberationFleet.Server.Application.Features.Auth.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;

public class RequestPasswordResetCommand : IRequest<PasswordResetResponse>
{
    public string Email { get; set; } = string.Empty;
}
