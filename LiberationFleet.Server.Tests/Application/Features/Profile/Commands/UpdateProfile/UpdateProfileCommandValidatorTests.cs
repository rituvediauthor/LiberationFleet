using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Profile.Commands.UpdateProfile;
using LiberationFleet.Server.Application.Features.Profile.Contracts;

namespace LiberationFleet.Server.Tests.Application.Features.Profile.Commands.UpdateProfile;

public class UpdateProfileCommandValidatorTests
{
    private readonly UpdateProfileCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCustomPlatformHasNameAndHandle_Passes()
    {
        var command = ValidCommand();
        command.PaymentPlatforms =
        [
            new PaymentPlatformAccountDto
            {
                PlatformId = 0,
                CustomPlatformName = "Revolut",
                Handle = "@user"
            }
        ];

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenCustomPlatformMissingName_Fails()
    {
        var command = ValidCommand();
        command.PaymentPlatforms =
        [
            new PaymentPlatformAccountDto
            {
                PlatformId = 0,
                Handle = "@user"
            }
        ];

        var result = _validator.TestValidate(command);

        result.ShouldHaveValidationErrorFor("PaymentPlatforms[0].PlatformId");
    }

    [Fact]
    public void Validate_WhenKnownPlatformSelected_Passes()
    {
        var command = ValidCommand();
        command.PaymentPlatforms =
        [
            new PaymentPlatformAccountDto
            {
                PlatformId = 2,
                Handle = "@user"
            }
        ];

        var result = _validator.TestValidate(command);

        result.ShouldNotHaveAnyValidationErrors();
    }

    private static UpdateProfileCommand ValidCommand() => new()
    {
        Username = "crewmate",
        Email = "crew@example.com",
        EmergencyLevel = 0
    };
}
