using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;
using LiberationFleet.Server.Domain.Entities;
using LiberationFleet.Server.Tests.TestHelpers;
using Microsoft.Extensions.Options;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandHandlerTests
{
    private static IOptions<EmailOptions> CreateEmailOptions() =>
        Options.Create(new EmailOptions
        {
            AppPublicBaseUrl = "https://localhost:49236"
        });

    [Fact]
    public async Task Handle_WhenUserExists_CreatesResetTokenSendsEmailAndReturnsGenericMessage()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var emailSender = new Mock<IEmailSender>();

        userRepository
            .Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        PasswordResetToken? capturedToken = null;
        tokenRepository
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Callback<PasswordResetToken, CancellationToken>((token, _) => capturedToken = token)
            .Returns(Task.CompletedTask);

        string? capturedBody = null;
        emailSender
            .Setup(s => s.SendAsync(
                user.Email,
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .Callback<string, string, string, CancellationToken>((_, _, body, _) => capturedBody = body)
            .Returns(Task.CompletedTask);

        var handler = new RequestPasswordResetCommandHandler(
            userRepository.Object,
            tokenRepository.Object,
            unitOfWork.Object,
            emailSender.Object,
            CreateEmailOptions(),
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

        capturedBody.Should().NotBeNullOrWhiteSpace();
        capturedBody.Should().Contain($"/reset-password?token={Uri.EscapeDataString(capturedToken.Token)}");

        tokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Once);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
        emailSender.Verify(s => s.SendAsync(
            user.Email,
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenEmailSendFails_StillReturnsSuccess()
    {
        var user = HandlerTestFixture.CreateUser();
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var emailSender = new Mock<IEmailSender>();

        userRepository
            .Setup(r => r.GetByEmailAsync("test@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync(user);

        tokenRepository
            .Setup(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()))
            .Returns(Task.CompletedTask);

        emailSender
            .Setup(s => s.SendAsync(
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<string>(),
                It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("SMTP unavailable"));

        var handler = new RequestPasswordResetCommandHandler(
            userRepository.Object,
            tokenRepository.Object,
            unitOfWork.Object,
            emailSender.Object,
            CreateEmailOptions(),
            HandlerTestFixture.CreateNullLogger<RequestPasswordResetCommandHandler>());

        var result = await handler.Handle(new RequestPasswordResetCommand
        {
            Email = "test@example.com"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task Handle_WhenUserDoesNotExist_ReturnsGenericMessageWithoutCreatingToken()
    {
        var userRepository = HandlerTestFixture.CreateUserRepositoryMock();
        var tokenRepository = HandlerTestFixture.CreatePasswordResetTokenRepositoryMock();
        var unitOfWork = HandlerTestFixture.CreateUnitOfWorkMock();
        var emailSender = new Mock<IEmailSender>();

        userRepository
            .Setup(r => r.GetByEmailAsync("missing@example.com", It.IsAny<CancellationToken>()))
            .ReturnsAsync((User?)null);

        var handler = new RequestPasswordResetCommandHandler(
            userRepository.Object,
            tokenRepository.Object,
            unitOfWork.Object,
            emailSender.Object,
            CreateEmailOptions(),
            HandlerTestFixture.CreateNullLogger<RequestPasswordResetCommandHandler>());

        var result = await handler.Handle(new RequestPasswordResetCommand
        {
            Email = "missing@example.com"
        }, CancellationToken.None);

        result.Success.Should().BeTrue();
        result.Message.Should().Contain("missing@example.com");

        tokenRepository.Verify(r => r.AddAsync(It.IsAny<PasswordResetToken>(), It.IsAny<CancellationToken>()), Times.Never);
        unitOfWork.Verify(u => u.SaveChangesAsync(It.IsAny<CancellationToken>()), Times.Never);
        emailSender.Verify(s => s.SendAsync(
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<string>(),
            It.IsAny<CancellationToken>()), Times.Never);
    }
}
