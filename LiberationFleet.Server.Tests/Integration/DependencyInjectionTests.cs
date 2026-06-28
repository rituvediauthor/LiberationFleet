using FluentValidation;
using LiberationFleet.Server.Application;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Application.Features.Auth.Commands.Login;
using LiberationFleet.Server.Application.Features.Auth.Commands.Register;
using LiberationFleet.Server.Application.Features.Crews.Commands.CreateCrew;
using LiberationFleet.Server.Application.Features.Crews.Queries.SearchCrews;
using LiberationFleet.Server.Infrastructure;
using LiberationFleet.Server.Tests.TestHelpers;
using MediatR;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Configuration;

namespace LiberationFleet.Server.Tests.Integration;

public class DependencyInjectionTests
{
    [Fact]
    public void AddApplication_RegistersMediatRHandlersAndValidators()
    {
        var services = new ServiceCollection();
        services.AddApplication();

        using var provider = services.BuildServiceProvider();

        provider.GetService<IMediator>().Should().NotBeNull();
        provider.GetServices<IValidator<RegisterCommand>>().Should().NotBeEmpty();
        provider.GetServices<IValidator<LoginCommand>>().Should().NotBeEmpty();
        provider.GetServices<IValidator<CreateCrewCommand>>().Should().NotBeEmpty();
        provider.GetServices<IValidator<SearchCrewsQuery>>().Should().NotBeEmpty();
    }

    [Fact]
    public void AddInfrastructure_RegistersPersistenceAndSecurityServices()
    {
        var configuration = TestConfiguration.CreateJwtConfiguration();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(configuration);
        services.AddInfrastructure(configuration);

        using var provider = services.BuildServiceProvider();

        provider.GetService<IUserRepository>().Should().NotBeNull();
        provider.GetService<IPasswordResetTokenRepository>().Should().NotBeNull();
        provider.GetService<ICrewRepository>().Should().NotBeNull();
        provider.GetService<ICrewMembershipRepository>().Should().NotBeNull();
        provider.GetService<IZipCodeDistanceService>().Should().NotBeNull();
        provider.GetService<ITokenService>().Should().NotBeNull();
        provider.GetService<IPasswordHasher>().Should().NotBeNull();
        provider.GetService<IUnitOfWork>().Should().NotBeNull();
    }

    [Fact]
    public async Task MediatRPipeline_WithInvalidRegisterCommand_ThrowsValidationException()
    {
        var services = new ServiceCollection();
        services.AddApplication();
        services.AddLogging();

        using var provider = services.BuildServiceProvider();
        var mediator = provider.GetRequiredService<IMediator>();

        var act = () => mediator.Send(new RegisterCommand());

        await act.Should().ThrowAsync<ValidationException>();
    }
}
