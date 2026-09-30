using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.ResendMfaLogin;

public record ResendMfaLoginCommand(ResendMfaLoginRequest Request) : IRequest<LoginResponse>;

public class ResendMfaLoginCommandHandler(
    IEmailMfaService emailMfaService) : IRequestHandler<ResendMfaLoginCommand, LoginResponse>
{
    public async Task<LoginResponse> Handle(ResendMfaLoginCommand request, CancellationToken cancellationToken)
    {
        var result = await emailMfaService.ResendAsync(request.Request.MfaChallengeToken, cancellationToken);
        if (!result.Success)
        {
            return new LoginResponse
            {
                Success = false,
                Message = result.Message,
                RequiresMfa = true,
                MfaChallengeToken = result.ChallengeToken
            };
        }

        // Ensure resend only for login challenges — Resend keeps the same challenge purpose.
        return new LoginResponse
        {
            Success = true,
            RequiresMfa = true,
            MfaChallengeToken = result.ChallengeToken,
            Message = result.Message
        };
    }
}
