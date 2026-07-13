using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Infrastructure.Realtime;
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
        services.AddScoped<IFleetRepository, FleetRepository>();
        services.AddScoped<ICrewInvitationRepository, CrewInvitationRepository>();
        services.AddScoped<IUserFleetRuleAcceptanceRepository, UserFleetRuleAcceptanceRepository>();
        services.AddScoped<ICrewMembershipRepository, CrewMembershipRepository>();
        services.AddScoped<ICrewCleanupRepository, CrewCleanupRepository>();
        services.AddScoped<IGiftRepository, GiftRepository>();
        services.AddScoped<IMutualAidRepository, MutualAidRepository>();
        services.AddScoped<IPaymentPlatformRepository, PaymentPlatformRepository>();
        services.AddScoped<ICrewPaymentPlatformRepository, CrewPaymentPlatformRepository>();
        services.AddScoped<ICryptoRepository, CryptoRepository>();
        services.AddScoped<IProposalRepository, ProposalRepository>();
        services.AddScoped<IForumRepository, ForumRepository>();
        services.AddScoped<IChatRepository, ChatRepository>();
        services.AddScoped<IRuleRepository, RuleRepository>();
        services.AddScoped<IFriendshipRepository, FriendshipRepository>();
        services.AddScoped<IUserBlockRepository, UserBlockRepository>();
        services.AddScoped<IDirectMessageRepository, DirectMessageRepository>();
        services.AddScoped<INotificationRepository, NotificationRepository>();
        services.AddScoped<IFallibleRepository, FallibleRepository>();
        services.AddScoped<ILibraryRepository, LibraryRepository>();
        services.AddScoped<IEmergencyRequestRepository, EmergencyRequestRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IVoicePresenceRepository, VoicePresenceRepository>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddScoped<IContentMentionRepository, ContentMentionRepository>();
        services.AddScoped<IVoicePresenceNotifier, VoicePresenceNotifier>();
        services.Configure<Infrastructure.LiveKit.LiveKitOptions>(configuration.GetSection(Infrastructure.LiveKit.LiveKitOptions.SectionName));
        services.AddSingleton<ILiveKitTokenService, Infrastructure.LiveKit.LiveKitTokenService>();
        services.AddHttpClient();
        services.AddSingleton<ILiveKitAdminService, Infrastructure.LiveKit.LiveKitAdminService>();
        services.AddSingleton<INotificationRealtimeNotifier, NotificationRealtimeNotifier>();
        services.AddSingleton<IChatRealtimeNotifier, ChatRealtimeNotifier>();
        services.AddSingleton<IDirectMessageRealtimeNotifier, DirectMessageRealtimeNotifier>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<Application.Services.MutualAidService>();
        services.AddScoped<IMutualAidService>(sp => sp.GetRequiredService<Application.Services.MutualAidService>());
        services.AddScoped<IMutualAidDevService>(sp => sp.GetRequiredService<Application.Services.MutualAidService>());
        services.AddSingleton<IZipCodeDistanceService, ZipCodeDistanceService>();
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        return services;
    }
}
