using FluentValidation;
using LiberationFleet.Server.Application.Common.Behaviors;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Rules;
using MediatR;

namespace LiberationFleet.Server.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        var assembly = typeof(DependencyInjection).Assembly;

        services.AddMediatR(cfg => cfg.RegisterServicesFromAssembly(assembly));
        services.AddValidatorsFromAssembly(assembly);
        services.AddTransient(typeof(IPipelineBehavior<,>), typeof(ValidationBehavior<,>));
        services.AddScoped<CrewSettingsProposalService>();
        services.AddScoped<CrewRulesProposalService>();
        services.AddScoped<CrewChatsProposalService>();

        return services;
    }
}
