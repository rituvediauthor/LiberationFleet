using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Auth.Commands.Register;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.Register;

public class RegisterCommandValidatorTests
{
    private readonly RegisterCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCommandIsValid_ShouldNotHaveErrors()
    {
        var command = new RegisterCommand
        {
            Username = "validuser",
            Email = "valid@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("", "valid@example.com", "password123", "password123", "Username")]
    [InlineData("validuser", "", "password123", "password123", "Email")]
    [InlineData("validuser", "not-an-email", "password123", "password123", "Email")]
    [InlineData("validuser", "valid@example.com", "", "password123", "Password")]
    [InlineData("validuser", "valid@example.com", "short", "short", "Password")]
    [InlineData("validuser", "valid@example.com", "password123", "", "ConfirmPassword")]
    public void Validate_WhenRequiredFieldsAreInvalid_ShouldHavePropertyError(
        string username,
        string email,
        string password,
        string confirmPassword,
        string expectedProperty)
    {
        var command = new RegisterCommand
        {
            Username = username,
            Email = email,
            Password = password,
            ConfirmPassword = confirmPassword
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(expectedProperty);
    }

    [Fact]
    public void Validate_WhenPasswordsDoNotMatch_ShouldHaveConfirmPasswordError()
    {
        var command = new RegisterCommand
        {
            Username = "validuser",
            Email = "valid@example.com",
            Password = "password123",
            ConfirmPassword = "different123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.ConfirmPassword)
            .WithErrorMessage("Passwords do not match");
    }

    [Fact]
    public void Validate_WhenUsernameExceedsMaxLength_ShouldHaveUsernameError()
    {
        var command = new RegisterCommand
        {
            Username = new string('a', 257),
            Email = "valid@example.com",
            Password = "password123",
            ConfirmPassword = "password123"
        };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Username);
    }
}
