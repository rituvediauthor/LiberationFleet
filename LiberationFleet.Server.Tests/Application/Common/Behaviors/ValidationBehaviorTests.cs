using FluentValidation;
using LiberationFleet.Server.Application.Common.Behaviors;
using LiberationFleet.Server.Application.Features.Auth.Commands.Login;
using MediatR;
using Moq;

namespace LiberationFleet.Server.Tests.Application.Common.Behaviors;

public class ValidationBehaviorTests
{
    [Fact]
    public async Task Handle_WhenNoValidatorsRegistered_InvokesNext()
    {
        var behavior = new ValidationBehavior<LoginCommand, string>(Array.Empty<IValidator<LoginCommand>>());
        var invoked = false;

        RequestHandlerDelegate<string> next = () =>
        {
            invoked = true;
            return Task.FromResult("ok");
        };

        var result = await behavior.Handle(new LoginCommand(), next, CancellationToken.None);

        invoked.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WhenValidationPasses_InvokesNext()
    {
        var validator = new Mock<IValidator<LoginCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult());

        var behavior = new ValidationBehavior<LoginCommand, string>(new[] { validator.Object });
        var invoked = false;

        RequestHandlerDelegate<string> next = () =>
        {
            invoked = true;
            return Task.FromResult("ok");
        };

        var result = await behavior.Handle(
            new LoginCommand { UsernameOrEmail = "user@example.com", Password = "password123" },
            next,
            CancellationToken.None);

        invoked.Should().BeTrue();
        result.Should().Be("ok");
    }

    [Fact]
    public async Task Handle_WhenValidationFails_ThrowsValidationException()
    {
        var validator = new Mock<IValidator<LoginCommand>>();
        validator
            .Setup(v => v.ValidateAsync(It.IsAny<ValidationContext<LoginCommand>>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new FluentValidation.Results.ValidationResult(new[]
            {
                new FluentValidation.Results.ValidationFailure("Password", "Password is required")
            }));

        var behavior = new ValidationBehavior<LoginCommand, string>(new[] { validator.Object });

        var act = () => behavior.Handle(
            new LoginCommand(),
            () => Task.FromResult("ok"),
            CancellationToken.None);

        await act.Should().ThrowAsync<ValidationException>();
    }
}
