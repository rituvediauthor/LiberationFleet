using System.Security.Cryptography;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Domain.Entities;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandler : IRequestHandler<RequestPasswordResetCommand, PasswordResetResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly IPasswordResetTokenRepository _passwordResetTokenRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly ILogger<RequestPasswordResetCommandHandler> _logger;

    public RequestPasswordResetCommandHandler(
        IUserRepository userRepository,
        IPasswordResetTokenRepository passwordResetTokenRepository,
        IUnitOfWork unitOfWork,
        ILogger<RequestPasswordResetCommandHandler> logger)
    {
        _userRepository = userRepository;
        _passwordResetTokenRepository = passwordResetTokenRepository;
        _unitOfWork = unitOfWork;
        _logger = logger;
    }

    public async Task<PasswordResetResponse> Handle(RequestPasswordResetCommand request, CancellationToken cancellationToken)
    {
        var message = $"If the email {request.Email} is in our system, a recovery message will be sent to it.";
        var user = await _userRepository.GetByEmailAsync(request.Email, cancellationToken);

        if (user is null)
        {
            _logger.LogInformation("Password reset requested for non-existent email: {Email}", request.Email);
            return new PasswordResetResponse { Success = true, Message = message };
        }

        var token = Convert.ToBase64String(RandomNumberGenerator.GetBytes(32));
        var resetToken = new PasswordResetToken
        {
            UserId = user.Id,
            Token = token,
            CreatedAt = DateTime.UtcNow,
            ExpiresAt = DateTime.UtcNow.AddHours(1),
            IsUsed = false
        };

        await _passwordResetTokenRepository.AddAsync(resetToken, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        _logger.LogInformation("Password reset token generated for user: {Email}", user.Email);

        return new PasswordResetResponse { Success = true, Message = message };
    }
}
