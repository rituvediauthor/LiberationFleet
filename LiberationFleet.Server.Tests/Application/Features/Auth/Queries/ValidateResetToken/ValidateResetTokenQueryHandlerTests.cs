using LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;
using LiberationFleet.Server.Tests.TestHelpers;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Queries.ValidateResetToken;

public class ValidateResetTokenQueryHandlerTests
{
    [Fact]
    public async Task Handle_WhenTokenIsValid_ReturnsValidResponseWithEmail()
    {
        var user = HandlerTestFixture.CreateUser(email: "reset@example.com");
        var resetToken = HandlerTestFixture.CreateResetToken(user, "valid-token");
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();

        tokenRepository
            .Setup(r => r.GetActiveByTokenAsync("valid-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        var handler = new ValidateResetTokenQueryHandler(tokenRepository.Object);

        var result = await handler.Handle(new ValidateResetTokenQuery { Token = "valid-token" }, CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.Message.Should().Be("Token is valid");
        result.Email.Should().Be("reset@example.com");
    }

    [Fact]
    public async Task Handle_WhenTokenNotFound_ReturnsInvalidResponse()
    {
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();

        tokenRepository
            .Setup(r => r.GetActiveByTokenAsync("missing-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync((Domain.Entities.PasswordResetToken?)null);

        var handler = new ValidateResetTokenQueryHandler(tokenRepository.Object);

        var result = await handler.Handle(new ValidateResetTokenQuery { Token = "missing-token" }, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be("Invalid or expired reset token");
        result.Email.Should().BeNull();
    }

    [Fact]
    public async Task Handle_WhenTokenIsExpired_ReturnsExpiredMessage()
    {
        var user = HandlerTestFixture.CreateUser();
        var resetToken = HandlerTestFixture.CreateResetToken(
            user,
            "expired-token",
            expiresAt: DateTime.UtcNow.AddMinutes(-5));

        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();

        tokenRepository
            .Setup(r => r.GetActiveByTokenAsync("expired-token", It.IsAny<CancellationToken>()))
            .ReturnsAsync(resetToken);

        var handler = new ValidateResetTokenQueryHandler(tokenRepository.Object);

        var result = await handler.Handle(new ValidateResetTokenQuery { Token = "expired-token" }, CancellationToken.None);

        result.IsValid.Should().BeFalse();
        result.Message.Should().Be("Reset token has expired");
    }
}
