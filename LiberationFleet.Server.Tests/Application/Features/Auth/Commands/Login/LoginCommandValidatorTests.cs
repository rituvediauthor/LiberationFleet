using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Auth.Commands.Login;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.Login;

public class LoginCommandValidatorTests
{
    private readonly LoginCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveErrors()
    {
        var command = new LoginCommand
        {
            UsernameOrEmail = "user@example.com",
            Password = "password123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenUsernameOrEmailIsEmpty_ShouldHaveError()
    {
        var command = new LoginCommand
        {
            UsernameOrEmail = "",
            Password = "password123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.UsernameOrEmail)
            .WithErrorMessage("Username or email is required");
    }

    [Fact]
    public void Validate_WhenPasswordIsEmpty_ShouldHaveError()
    {
        var command = new LoginCommand
        {
            UsernameOrEmail = "user@example.com",
            Password = ""
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Password)
            .WithErrorMessage("Password is required");
    }
}
