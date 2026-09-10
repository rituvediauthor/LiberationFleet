using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Commands.CreateCrew;

public class CreateCrewCommandValidatorTests
{
    private readonly CreateCrewCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenOnlineCrewIsValid_ShouldNotHaveErrors()
    {
        var command = new CreateCrewCommand
        {
            Name = "Fleet Alpha",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Online"
        };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenLocalCrewIsValid_ShouldNotHaveErrors()
    {
        var command = new CreateCrewCommand
        {
            Name = "Local Fleet",
            MaxSize = 5,
            Privacy = "Private",
            Scope = "Local",
            CountryCode = "US",
            AllowedZipCodes = ["90210"]
        };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenLocalCrewHasAlphanumericPostal_ShouldNotHaveErrors()
    {
        var command = new CreateCrewCommand
        {
            Name = "London Crew",
            MaxSize = 5,
            Privacy = "Public",
            Scope = "Local",
            CountryCode = "GB",
            AllowedZipCodes = ["SW1A 1AA"]
        };

        _validator.TestValidate(command).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenNameIsEmpty_ShouldHaveNameError()
    {
        var command = new CreateCrewCommand
        {
            Name = "",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Online"
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Name);
    }

    [Fact]
    public void Validate_WhenPrivacyIsInvalid_ShouldHavePrivacyError()
    {
        var command = new CreateCrewCommand
        {
            Name = "Fleet",
            MaxSize = 10,
            Privacy = "Invalid",
            Scope = "Online"
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Privacy);
    }

    [Fact]
    public void Validate_WhenScopeIsInvalid_ShouldHaveScopeError()
    {
        var command = new CreateCrewCommand
        {
            Name = "Fleet",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Invalid"
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.Scope);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(101)]
    public void Validate_WhenMaxSizeOutOfRange_ShouldHaveMaxSizeError(int maxSize)
    {
        var command = new CreateCrewCommand
        {
            Name = "Fleet",
            MaxSize = maxSize,
            Privacy = "Public",
            Scope = "Online"
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.MaxSize);
    }

    [Fact]
    public void Validate_WhenLocalScopeMissingZipCodes_ShouldHaveAllowedZipCodesError()
    {
        var command = new CreateCrewCommand
        {
            Name = "Local Fleet",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Local",
            CountryCode = "US"
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AllowedZipCodes);
    }

    [Fact]
    public void Validate_WhenLocalScopeMissingCountry_ShouldHaveAllowedZipCodesError()
    {
        var command = new CreateCrewCommand
        {
            Name = "Local Fleet",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Local",
            AllowedZipCodes = ["90210"]
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AllowedZipCodes);
    }

    [Fact]
    public void Validate_WhenOnlineScopeHasZipCodes_ShouldHaveAllowedZipCodesError()
    {
        var command = new CreateCrewCommand
        {
            Name = "Online Fleet",
            MaxSize = 10,
            Privacy = "Public",
            Scope = "Online",
            AllowedZipCodes = ["90210"]
        };

        _validator.TestValidate(command).ShouldHaveValidationErrorFor(x => x.AllowedZipCodes);
    }
}
