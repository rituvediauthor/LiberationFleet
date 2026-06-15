using LiberationFleet.Server.Application.Features.Auth.Commands.Login;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.Login;

public class LoginCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenCredentialsAreValid_ReturnsTokenAndUpdatesLastLogin()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock(verifyResult: true);
        var tokenService = HandlerTestFixture.CreateTokenServiceMock();

        userRepository
            .Setup(r => r.GetByEmailOrUsernameAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        userRepository
            .Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new LoginCommandHandler(
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            tokenService.Object,
            HandlerTestFixture.CreateNullLogger<LoginCommandHandler>());

        var result = await handler.Handle(new LoginCommand
        {
            UsernameOrEmail = "test@example.com",
            Password = "password123"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Login successful");
        result.Token.Should().Be("jwt-token");
        result.User!.Email.Should().Be("test@example.com");

        user.LastLoginAt.Should().NotBeNull();
        userRepository.Verify(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        passwordHasher.Verify(h => h.Verify("password123", user.PasswordHash), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserNotFound_ReturnsInvalidCredentials()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock();
        var tokenService = HandlerTestFixture.CreateTokenServiceMock();

        userRepository
            .Setup(r => r.GetByEmailOrUsernameAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.User?)null);

        var handler = new LoginCommandHandler(
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            tokenService.Object,
            HandlerTestFixture.CreateNullLogger<LoginCommandHandler>());

        var result = await handler.Handle(new LoginCommand
        {
            UsernameOrEmail = "missing@example.com",
            Password = "password123"
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        tokenService.Verify(t => t.GenerateJwtToken(It.IsAny<Domain.Entities.User>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenPasswordIsInvalid_ReturnsInvalidCredentials()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock(verifyResult: false);
        var tokenService = HandlerTestFixture.CreateTokenServiceMock();

        userRepository
            .Setup(r => r.GetByEmailOrUsernameAsync("testuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        var handler = new LoginCommandHandler(
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            tokenService.Object,
            HandlerTestFixture.CreateNullLogger<LoginCommandHandler>());

        var result = await handler.Handle(new LoginCommand
        {
            UsernameOrEmail = "testuser",
            Password = "wrong-password"
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid credentials");

        userRepository.Verify(r => r.UpdateAsync(It.IsAny<Domain.Entities.User>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
