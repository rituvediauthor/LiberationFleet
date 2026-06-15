using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Auth.Commands.RequestPasswordReset;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Commands.RequestPasswordReset;

public class RequestPasswordResetCommandValidatorTests
{
    private readonly RequestPasswordResetCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenEmailIsValid_ShouldNotHaveErrors()
    {
        var command = new RequestPasswordResetCommand { Email = "user@example.com" };

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-an-email")]
    public void Validate_WhenEmailIsInvalid_ShouldHaveEmailError(string email)
    {
        var command = new RequestPasswordResetCommand { Email = email };

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor(x => x.Email);
    }
}
