using LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenUserExists_CreatesResetTokenAndReturnsGenericMessage()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        userRepository
            .Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        PasswordResetToken? capturedToken = null;
        tokenRepository
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((token, _) => capturedToken = token)
            .Returns(Task.CompletedTask);

        var handler = new RequestPasswordResetCommandHandler(
            userRepository.Object,
            tokenRepository.Object,
            unitOfWork.Object,
            HandlerTestFixture.CreateNullLogger<RequestPasswordResetCommandHandler>());

        var result = await handler.Handle(new RequestPasswordResetCommand
        {
            Email = "test@example.com"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("test@example.com");

        capturedToken.Should().NotBeNull();
        capturedToken!.UserId.Should().Be(user.Id);
        capturedToken.Token.Should().NotBeNullOrWhiteSpace();
        capturedToken.IsUsed.Should().BeFalse();
        capturedToken.ExpiresAt.Should().BeAfter(DateTime.UtcNow);

        tokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsGenericMessageWithoutCreatingToken()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();

        userRepository
            .Setup(r => r.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new RequestPasswordResetCommandHandler(
            userRepository.Object,
            tokenRepository.Object,
            unitOfWork.Object,
            HandlerTestFixture.CreateNullLogger<RequestPasswordResetCommandHandler>());

        var result = await handler.Handle(new RequestPasswordResetCommand
        {
            Email = "missing@example.com"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("missing@example.com");

        tokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }
}
