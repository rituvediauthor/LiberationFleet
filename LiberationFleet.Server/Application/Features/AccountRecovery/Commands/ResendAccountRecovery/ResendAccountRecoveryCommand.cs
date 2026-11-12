using LiberationFleet.Server.Application.Features.AccountRecovery.Contracts;
using LiberationFleet.Server.Application.Services;
using MediatR;

namespace LiberationFleet.Server.Application.Features.AccountRecovery.Commands.ResendAccountRecovery;

public record ResendAccountRecoveryCommand(ResendAccountRecoveryRequest Request)
    : IRequest<AccountRecoveryChallengeResponse>;

public class ResendAccountRecoveryCommandHandler(
    IEmailMfaService emailMfaService) : IRequestHandler<ResendAccountRecoveryCommand, AccountRecoveryChallengeResponse>
{
    public async Task<AccountRecoveryChallengeResponse> Handle(
        ResendAccountRecoveryCommand request,
        CancellationToken cancellationToken)
    {
        var token = request.Request.ChallengeToken?.Trim() ?? string.Empty;
        if (string.IsNullOrEmpty(token))
        {
            return new AccountRecoveryChallengeResponse
            {
                Success = false,
                Message = "Verification session is missing. Start recovery again."
            };
        }

        // ResendAsync does not filter by purpose; verify after via a peek is not available.
        // Complete will enforce AccountRecovery purpose.
        var sent = await emailMfaService.ResendAsync(token, cancellationToken);
        return new AccountRecoveryChallengeResponse
        {
            Success = sent.Success,
            Message = sent.Message,
            ChallengeToken = sent.ChallengeToken
        };
    }
}
