using LiberationFleet.Server.Application.Features.Auth.Commands.Register;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.Register;

public class RegisterCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserDoesNotExist_RegistersUserAndReturnsToken()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock();
        var tokenService = HandlerTestFixture.CreateTokenServiceMock();

        userRepository
            .Setup(r => r.ExistsByEmailOrUsernameAsync("new@example.com", "newuser", It.IsAny<CancellationToken>()))
            .ReturnsAsync(false);

        User? capturedUser = null;
        userRepository
            .Setup(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()))
            .Callback<User, CancellationToken>((user, _) => capturedUser = user)
            .Returns(Task.CompletedTask);

        var handler = new RegisterCommandHandler(
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            tokenService.Object,
            HandlerTestFixture.CreateNullLogger<RegisterCommandHandler>());

        var command = new RegisterCommand
        {
            Username = "newuser",
            Email = "new@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        var result = await handler.Handle(command, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Registration successful");
        result.Token.Should().Be("jwt-token");
        result.User.Should().NotBeNull();
        result.User!.Username.Should().Be("newuser");
        result.User.Email.Should().Be("new@example.com");

        capturedUser.Should().NotBeNull();
        capturedUser!.PasswordHash.Should().Be("hashed-password");
        capturedUser.IsActive.Should().BeTrue();

        userRepository.Verify(r => r.ExistsByEmailOrUsernameAsync("new@example.com", "newuser", It.IsAny<CancellationToken>()), Times.Once);
        userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        passwordHasher.Verify(h => h.Hash("password123"), Times.Once);
        tokenService.Verify(t => t.GenerateJwtToken(It.IsAny<User>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailOrUsernameAlreadyExists_ReturnsFailureWithoutPersisting()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock();
        var tokenService = HandlerTestFixture.CreateTokenServiceMock();

        userRepository
            .Setup(r => r.ExistsByEmailOrUsernameAsync("existing@example.com", "existing", It.IsAny<CancellationToken>()))
            .ReturnsAsync(true);

        var handler = new RegisterCommandHandler(
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            tokenService.Object,
            HandlerTestFixture.CreateNullLogger<RegisterCommandHandler>());

        var result = await handler.Handle(new RegisterCommand
        {
            Username = "existing",
            Email = "existing@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Email or username already registered");
        result.Token.Should().BeNull();

        userRepository.Verify(r => r.AddAsync(It.IsAny<User>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        passwordHasher.Verify(h => h.Hash(It.IsAny<string>()), Times.Never);
        tokenService.Verify(t => t.GenerateJwtToken(It.IsAny<User>()), Times.Never);
    }
}
