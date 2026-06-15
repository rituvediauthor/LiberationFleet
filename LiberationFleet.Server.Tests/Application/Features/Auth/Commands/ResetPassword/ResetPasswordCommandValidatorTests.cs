using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Auth.Commands.ResetPassword;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.ResetPassword;

public class ResetPasswordCommandValidatorTests
{
    private readonly ResetPasswordCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveErrors()
    {
        var command = new ResetPasswordCommand
        {
            Token = "reset-token",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenTokenIsEmpty_ShouldHaveTokenError()
    {
        var command = new ResetPasswordCommand
        {
            Token = "",
            NewPassword = "newpassword123",
            ConfirmPassword = "newpassword123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage("Token is required");
    }

    [Fact]
    public void Validate_WhenNewPasswordIsTooShort_ShouldHaveNewPasswordError()
    {
        var command = new ResetPasswordCommand
        {
            Token = "reset-token",
            NewPassword = "short",
            ConfirmPassword = "short"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.NewPassword)
            .WithErrorMessage("Password must be at least 8 characters");
    }

    [Fact]
    public void Validate_WhenPasswordsDoNotMatch_ShouldHaveConfirmPasswordError()
    {
        var command = new ResetPasswordCommand
        {
            Token = "reset-token",
            NewPassword = "newpassword123",
            ConfirmPassword = "different123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match");
    }
}
