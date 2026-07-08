using LiberationFleet.Server.Application.Features.Auth.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.Login;

public class LoginCommand : IRequest<LoginResponse>
{
    public string UsernameOrEmail { get; set; } = string.Empty;
    public string Password { get; set; } = string.Empty;
    public string? DeviceId { get; set; }
    public string? DeviceName { get; set; }
    public string? UserAgent { get; set; }
}
