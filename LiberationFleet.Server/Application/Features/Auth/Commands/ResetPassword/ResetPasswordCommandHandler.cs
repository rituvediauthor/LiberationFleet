using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandler : IRequestHandler<ResetPasswordCommand, PasswordResetResponse>
{
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUserRepository _userRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ILogger<ResetPasswordCommandHandler> _logger;

    public ResetPasswordCommandHandler(
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUserRepository userRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ILogger<ResetPasswordCommandHandler> logger)
    {
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _userRepository = userRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _logger = logger;
    }

    public async Task<PasswordResetResponse> Handle(ResetPasswordCommand request, CancellationToken cancellationToken)
    {
        var resetToken = await _passwordResetTokenRepository.GetActiveByTokenAsync(request.Token, cancellationToken);

        if (resetToken is null || resetToken.ExpiresAt < DateTime.UtcNow)
        {
            return new PasswordResetResponse
            {
                Success = false,
                Message = "Invalid or expired reset token"
            };
        }

        var user = resetToken.User;
        user.PasswordHash = _passwordHasher.Hash(request.NewPassword);

        resetToken.IsUsed = true;
        resetToken.UsedAt = DateTime.UtcNow;

        await _userRepository.UpdateAsync(user, cancellationToken);
        await _passwordResetTokenRepository.UpdateAsync(resetToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset for user: {Email}", user.Email);

        return new PasswordResetResponse
        {
            Success = true,
            Message = "Password reset successfully"
        };
    }
}
