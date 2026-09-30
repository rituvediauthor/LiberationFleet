using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Infrastructure.Realtime;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Infrastructure.Data;
using LiberationFleet.Server.Infrastructure.Email;
using LiberationFleet.Server.Infrastructure.Persistence.Repositories;
using LiberationFleet.Server.Infrastructure.Security;
using LiberationFleet.Server.Infrastructure.Services;
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
        services.AddScoped<IEmailMfaChallengeRepository, EmailMfaChallengeRepository>();
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
        services.AddScoped<ILibraryTaskRepository, LibraryTaskRepository>();
        services.AddScoped<IEmergencyRequestRepository, EmergencyRequestRepository>();
        services.AddScoped<IUserActivityRepository, UserActivityRepository>();
        services.AddScoped<IVoicePresenceRepository, VoicePresenceRepository>();
        services.AddScoped<ISecurityRepository, SecurityRepository>();
        services.AddScoped<IContentMentionRepository, ContentMentionRepository>();
        services.AddScoped<IContentTenureRepository, ContentTenureRepository>();
        services.AddScoped<IContentReportRepository, ContentReportRepository>();
        services.AddScoped<IAppDonationRepository, AppDonationRepository>();
        services.AddScoped<IVoicePresenceNotifier, VoicePresenceNotifier>();
        services.Configure<Infrastructure.LiveKit.LiveKitOptions>(configuration.GetSection(Infrastructure.LiveKit.LiveKitOptions.SectionName));
        services.Configure<Application.Services.ReportEvidenceOptions>(
            configuration.GetSection(Application.Services.ReportEvidenceOptions.SectionName));
        services.Configure<Application.Services.StripeDonationOptions>(
            configuration.GetSection(Application.Services.StripeDonationOptions.SectionName));
        services.Configure<Application.Services.MediaDeepFreezeOptions>(
            configuration.GetSection(Application.Services.MediaDeepFreezeOptions.SectionName));
        services.Configure<Background.BackgroundJobsOptions>(
            configuration.GetSection(Background.BackgroundJobsOptions.SectionName));
        services.AddSingleton<Application.Services.IReportEvidenceProtector, Application.Services.ReportEvidenceProtector>();
        services.AddHttpClient(nameof(Application.Services.ReportVendorNotifier));
        services.AddSingleton<Application.Services.IReportVendorNotifier, Application.Services.ReportVendorNotifier>();
        services.AddSingleton<Storage.LocalDeepFreezeBlobStore>();
        services.AddSingleton<Storage.AzureDeepFreezeBlobStore>();
        services.AddSingleton<Storage.NullDeepFreezeBlobStore>();
        services.AddSingleton<IDeepFreezeBlobStore, Storage.DeepFreezeBlobStoreRouter>();
        services.AddScoped<Application.Services.IMediaDeepFreezeService, Application.Services.MediaDeepFreezeService>();
        services.AddSingleton<Background.ActivityTriggeredBackgroundJobs>();
        services.AddHostedService<Background.ContentReportRetentionHostedService>();
        services.AddHostedService<Background.MediaDeepFreezeHostedService>();
        services.AddHostedService<Background.GiftAutoVerifyHostedService>();
        services.AddHostedService<Background.ProposalTimerHostedService>();
        services.AddSingleton<Data.DevEnvironmentResetService>();
        services.AddSingleton<ILiveKitTokenService, Infrastructure.LiveKit.LiveKitTokenService>();
        services.AddHttpClient();
        services.AddSingleton<ILiveKitAdminService, Infrastructure.LiveKit.LiveKitAdminService>();
        services.AddSingleton<INotificationRealtimeNotifier, NotificationRealtimeNotifier>();
        services.AddSingleton<IChatRealtimeNotifier, ChatRealtimeNotifier>();
        services.AddSingleton<IDirectMessageRealtimeNotifier, DirectMessageRealtimeNotifier>();
        services.AddScoped<ICurrentUserService, CurrentUserService>();
        services.AddScoped<ViewerLocationAccessor>();
        services.AddScoped<IViewerLocationAccessor>(sp => sp.GetRequiredService<ViewerLocationAccessor>());
        services.AddScoped<Application.Services.MutualAidService>();
        services.AddScoped<IMutualAidService>(sp => sp.GetRequiredService<Application.Services.MutualAidService>());
        services.AddScoped<IMutualAidDevService>(sp => sp.GetRequiredService<Application.Services.MutualAidService>());
        services.AddScoped<ITokenService, JwtTokenService>();
        services.AddSingleton<IPasswordHasher, BcryptPasswordHasher>();

        services.Configure<EmailOptions>(configuration.GetSection(EmailOptions.SectionName));
        services.Configure<Application.Services.EmailMfaOptions>(
            configuration.GetSection(Application.Services.EmailMfaOptions.SectionName));
        services.AddScoped<Application.Services.IEmailMfaService, Application.Services.EmailMfaService>();
        RegisterEmailSender(services, configuration);

        return services;
    }

    private static void RegisterEmailSender(IServiceCollection services, IConfiguration configuration)
    {
        var smtpHost = configuration.GetSection(EmailOptions.SectionName)["SmtpHost"];
        var envName = configuration["ASPNETCORE_ENVIRONMENT"]
            ?? Environment.GetEnvironmentVariable("ASPNETCORE_ENVIRONMENT")
            ?? "Production";
        var allowsLogOnly = string.Equals(envName, "Development", StringComparison.OrdinalIgnoreCase)
            || string.Equals(envName, "Docker", StringComparison.OrdinalIgnoreCase);

        if (string.IsNullOrWhiteSpace(smtpHost))
        {
            if (!allowsLogOnly)
            {
                throw new InvalidOperationException(
                    "Email:SmtpHost is required in Staging/Production. " +
                    "Without it, password-reset emails are only logged and never delivered. " +
                    "Set Email__SmtpHost (and related Email__* settings) on the App Service — see docs/AZURE-GO-LIVE.md.");
            }

            services.AddSingleton<IEmailSender, LogEmailSender>();
            return;
        }

        var fromAddress = configuration.GetSection(EmailOptions.SectionName)["FromAddress"];
        var appBaseUrl = configuration.GetSection(EmailOptions.SectionName)["AppPublicBaseUrl"];
        if (!allowsLogOnly
            && (string.IsNullOrWhiteSpace(fromAddress) || string.IsNullOrWhiteSpace(appBaseUrl)))
        {
            throw new InvalidOperationException(
                "Email:FromAddress and Email:AppPublicBaseUrl are required when Email:SmtpHost is set in Staging/Production.");
        }

        services.AddSingleton<IEmailSender, SmtpEmailSender>();
    }
}
