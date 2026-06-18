using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Infrastructure.Data;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Infrastructure.Geocoding;
using LiberationFleet.Server.Infrastructure.Security;
using Microsoft.EntityFrameworkCore;

namespace LiberationFleet.Server.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        services.AddDbContext<ApplicationDbContext>(options =>
            options.UseSqlServer(
                configuration.GetConnectionString("DefaultConnection") ??
                "Server=(localdb)\\mssqllocaldb;Database=LiberationFleetDb;Trusted_Connection=true;",
                sqlOptions => sqlOptions.EnableRetryOnFailure(
                    maxRetryCount: 5,
                    maxRetryDelay: TimeSpan.FromSeconds(10),
                    errorNumbersToAdd: null)));

        services.AddHttpContextAccessor();
        services.AddScoped<IUnitOfWork>(sp => sp.GetRequiredService<ApplicationDbContext>());
        services.AddScoped<IUserRepository, UserRepository>();
        services.AddScoped<IPasswordResetTokenRepository, PasswordResetTokenRepository>();
        services.AddScoped<ICrewRepository, CrewRepository>();
        services.AddScoped<ICrewMembershipRepository, CrewMembershipRepository>();
        services.AddScoped<IGiftRepository, GiftRepository>();
        services.AddScoped<IMutualAidRepository, MutualAidRepository>();
        services.AddScoped<IPaymentPlatformRepository, PaymentPlatformRepository>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<IMutualAidService, Application.Services.MutualAidService>();
        services.AddSingleton<IZipCodeDistanceService, ZipCodeDistanceService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        return services;
    }
}
