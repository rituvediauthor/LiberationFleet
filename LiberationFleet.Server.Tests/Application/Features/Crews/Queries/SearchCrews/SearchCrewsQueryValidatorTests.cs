using FluentValidation.TestHelper;
using LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;

namespace LiberationFleet.Server.Tests.Application.Features.Crews.Queries.SearchCrews;

public class SearchCrewsQueryValidatorTests
{
    private readonly SearchCrewsQueryValidator _validator = new();

    [Fact]
    public void Validate_WhenOnlineSearchIsValid_ShouldNotHaveErrors()
    {
        var query = new SearchCrewsQuery { Scope = "Online", Page = 1, PageSize = 10 };

        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenLocalSearchIsValid_ShouldNotHaveErrors()
    {
        var query = new SearchCrewsQuery
        {
            Scope = "Local",
            Page = 1,
            PageSize = 10
        };

        _validator.TestValidate(query).ShouldNotHaveAnyValidationErrors();
    }

    [Fact]
    public void Validate_WhenScopeIsInvalid_ShouldHaveScopeError()
    {
        _validator.TestValidate(new SearchCrewsQuery { Scope = "Global" })
            .ShouldHaveValidationErrorFor(x => x.Scope);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(51)]
    public void Validate_WhenPageSizeOutOfRange_ShouldHavePageSizeError(int pageSize)
    {
        _validator.TestValidate(new SearchCrewsQuery { Scope = "Online", PageSize = pageSize })
            .ShouldHaveValidationErrorFor(x => x.PageSize);
    }
}
