using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;

public class ValidateResetTokenQueryHandler : IRequestHandler<ValidateResetTokenQuery, ValidateResetTokenResponse>
{
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;

    public ValidateResetTokenQueryHandler(IPasswordResetTokenRepository passwordResetTokenRepository)
    {
        _passwordResetTokenRepository = passwordResetTokenRepository;
    }

    public async Task<ValidateResetTokenResponse> Handle(ValidateResetTokenQuery request, CancellationToken cancellationToken)
    {
        var resetToken = await _passwordResetTokenRepository.GetActiveByTokenAsync(request.Token, cancellationToken);

        if (resetToken is null)
        {
            return new ValidateResetTokenResponse
            {
                IsValid = false,
                Message = "Invalid or expired reset token"
            };
        }

        if (resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return new ValidateResetTokenResponse
            {
                IsValid = false,
                Message = "Reset token has expired"
            };
        }

        return new ValidateResetTokenResponse
        {
            IsValid = true,
            Message = "Token is valid",
            Email = resetToken.User.Email
        };
    }
}
