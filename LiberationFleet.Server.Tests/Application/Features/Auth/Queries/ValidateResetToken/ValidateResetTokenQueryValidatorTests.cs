using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Auth.Queries.ValidateResetToken;

namespace LiberationFleet.Server.Tests.Application.Features.Auth.Queries.ValidateResetToken;

public class ValidateResetTokenQueryValidatorTests
{
    private readonly ValidateResetTokenQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenTokenIsProvided_ShouldNotHaveErrors()
    {
        var query = new ValidateResetTokenQuery { Token = "some-token" };

        var result = _validator.TestValidate(query);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenTokenIsEmpty_ShouldHaveTokenError()
    {
        var query = new ValidateResetTokenQuery { Token = "" };

        var result = _validator.TestValidate(query);

        result.ShouldHaveValidationErrorFor(x => x.Token)
            .WithErrorMessage("Token is required");
    }
}
