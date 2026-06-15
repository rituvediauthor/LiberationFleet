using LiberationFleet.Server.Application.Features.Auth.Commands.ResetPassword;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandHandlerTests
{
    [Fact]
    public async Task Handle_WhenTokenIsValid_ResetsPasswordAndMarksTokenUsed()
    {
        var user = HandlerTestFixture.CreateUser();
        var resetToken = HandlerTestFixture.CreateResetToken(user, "valid-token");
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock("new-hash");

        tokenRepository
            .Setup(r => r.GetActiveByTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        userRepository
            .Setup(r => r.UpdateAsync(user, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        tokenRepository
            .Setup(r => r.UpdateAsync(resetToken, It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        var handler = new ResetPasswordCommandHandler(
            tokenRepository.Object,
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            HandlerTestFixture.CreateNullLogger<ResetPasswordCommandHandler>());

        var result = await handler.Handle(new ResetPasswordCommand
        {
            Token = "valid-token",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Be("Password reset successfully");

        user.PasswordHash.Should().Be("new-hash");
        resetToken.IsUsed.Should().BeTrue();
        resetToken.UsedAt.Should().NotBeNull();

        passwordHasher.Verify(h => h.Hash("newpassword123"), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenTokenNotFound_ReturnsFailure()
    {
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock();

        tokenRepository
            .Setup(r => r.GetActiveByTokenAsync("missing-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.PasswordResetToken?)null);

        var handler = new ResetPasswordCommandHandler(
            tokenRepository.Object,
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            HandlerTestFixture.CreateNullLogger<ResetPasswordCommandHandler>());

        var result = await handler.Handle(new ResetPasswordCommand
        {
            Token = "missing-token",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired reset token");

        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_WhenTokenIsExpired_ReturnsFailure()
    {
        var user = HandlerTestFixture.CreateUser();
        var resetToken = HandlerTestFixture.CreateResetToken(
            user,
            "expired-token",
            expiresAt: DateTime.UtcNow.AddHours(-1));

        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var passwordHasher = HandlerTestFixture.CreatePasswordHasherMock();

        tokenRepository
            .Setup(r => r.GetActiveByTokenAsync("expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        var handler = new ResetPasswordCommandHandler(
            tokenRepository.Object,
            userRepository.Object,
            unitOfWork.Object,
            passwordHasher.Object,
            HandlerTestFixture.CreateNullLogger<ResetPasswordCommandHandler>());

        var result = await handler.Handle(new ResetPasswordCommand
        {
            Token = "expired-token",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        }, CancellationToken.None);

        result.Success.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired reset token");
    }
}
