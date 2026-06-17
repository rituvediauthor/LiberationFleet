using FluentValidation;
using LiberationFleet.Server.Application.Common.Behaviors;
using LiberationFleet.Server.Application.Services;
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

        services.AddScoped<IMutualAidCalculationService, MutualAidCalculationService>();
        services.AddScoped<IReceptionOrderService, ReceptionOrderService>();

        return services;
    }
}
