using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Crews.Commands.JoinCrew;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Commands.JoinCrew;

public class JoinCrewCommandValidatorTests
{
    private readonly JoinCrewCommandValidator _validator = new();

    [Fact]
    public void Validate_WhenCrewIdProvided_ShouldNotHaveErrors()
    {
        _validator.TestValidate(new JoinCrewCommand { CrewId = 1 }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenJoinCodeProvided_ShouldNotHaveErrors()
    {
        _validator.TestValidate(new JoinCrewCommand { JoinCode = "ABC12345" }).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenNeitherCrewIdNorJoinCodeProvided_ShouldHaveError()
    {
        _validator.TestValidate(new JoinCrewCommand()).ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_WhenBothCrewIdAndJoinCodeProvided_ShouldHaveError()
    {
        _validator.TestValidate(new JoinCrewCommand { CrewId = 1, JoinCode = "ABC12345" })
            .ShouldHaveValidationErrorFor(x => x);
    }

    [Fact]
    public void Validate_WhenJoinCodeTooShort_ShouldHaveJoinCodeError()
    {
        _validator.TestValidate(new JoinCrewCommand { JoinCode = "abc" })
            .ShouldHaveValidationErrorFor(x => x.JoinCode);
    }

    [Fact]
    public void Validate_WhenJoinCodeMissing_ShouldHaveCommandError()
    {
        _validator.TestValidate(new JoinCrewCommand())
            .ShouldHaveValidationErrorFor(x => x);
    }
}
