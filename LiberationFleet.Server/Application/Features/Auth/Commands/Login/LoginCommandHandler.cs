using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Commands.Login;
using LiberationFleet.Server.Application.Features.Auth.Contracts;
using LiberationFleet.Server.Application.Features.Security.Commands.RecordLoginAttempt;
using MediatR;

namespace LiberationFleet.Server.Application.Features.Auth.Commands.Login;

public class LoginCommandHandler : IRequestHandler<LoginCommand, LoginResponse>
{
    private readonly IUserRepository _userRepository;
    private readonly ISecurityRepository _securityRepository;
    private readonly IUnitOfWork _unitOfWork;
    private readonly IPasswordHasher _passwordHasher;
    private readonly ITokenService _tokenService;
    private readonly IMediator _mediator;
    private readonly ILogger<LoginCommandHandler> _logger;

    public LoginCommandHandler(
        IUserRepository userRepository,
        ISecurityRepository securityRepository,
        IUnitOfWork unitOfWork,
        IPasswordHasher passwordHasher,
        ITokenService tokenService,
        IMediator mediator,
        ILogger<LoginCommandHandler> logger)
    {
        _userRepository = userRepository;
        _securityRepository = securityRepository;
        _unitOfWork = unitOfWork;
        _passwordHasher = passwordHasher;
        _tokenService = tokenService;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<LoginResponse> Handle(LoginCommand request, CancellationToken cancellationToken)
    {
        var user = await _userRepository.GetByEmailOrUsernameAsync(request.UsernameOrEmail, cancellationToken);
        var credentialsValid = user is not null
            && !user.IsUnclaimedPlaceholder
            && _passwordHasher.Verify(request.Password, user.PasswordHash);

        if (!credentialsValid)
        {
            await _mediator.Send(new RecordLoginAttemptCommand(
                user?.Id,
                request.UsernameOrEmail,
                Success: false,
                request.DeviceId,
                request.DeviceName,
                request.UserAgent), cancellationToken);

            return new LoginResponse
            {
                Success = false,
                Message = "Invalid credentials"
            };
        }

        if (!user!.IsActive)
        {
            return new LoginResponse
            {
                Success = false,
                Message = "This account has been frozen pending a safety review."
            };
        }

        if (!string.IsNullOrWhiteSpace(request.DeviceId))
        {
            var device = await _securityRepository.GetDeviceByDeviceIdAsync(user!.Id, request.DeviceId.Trim(), cancellationToken);
            if (device?.IsBlocked == true)
            {
                return new LoginResponse
                {
                    Success = false,
                    Message = "This device has been blocked from signing in."
                };
            }
        }

        user!.LastLoginAt = DateTime.UtcNow;
        await _userRepository.UpdateAsync(user, cancellationToken);
        await _unitOfWork.SaveChangesAsync(cancellationToken);

        await _mediator.Send(new RecordLoginAttemptCommand(
            user.Id,
            request.UsernameOrEmail,
            Success: true,
            request.DeviceId,
            request.DeviceName,
            request.UserAgent), cancellationToken);

        _logger.LogInformation("User logged in: {Email}", user.Email);

        return new LoginResponse
        {
            Success = true,
            Message = "Login successful",
            Token = _tokenService.GenerateJwtToken(user),
            User = new UserDto
            {
                Id = user.Id,
                Username = user.Username,
                Email = user.Email
            }
        };
    }
}
