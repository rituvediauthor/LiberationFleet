using FluentValidation;
using LiberationFleet.Server.Application.Common.Behaviors;
using LiberationFleet.Server.Application.Features.Crews;
using LiberationFleet.Server.Application.Features.EmergencyRequests;
using LiberationFleet.Server.Application.Features.Chats;
using LiberationFleet.Server.Application.Features.Library;
using LiberationFleet.Server.Application.Features.Mentions;
using LiberationFleet.Server.Application.Features.Notifications;
using LiberationFleet.Server.Application.Features.Proposals;
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
        services.AddScoped<CrewmateKickProposalService>();
        services.AddScoped<CrewmateRejoinProposalService>();
        services.AddScoped<CrewJoinRequestProposalService>();
        services.AddScoped<CrewRoleProposalService>();
        services.AddScoped<ClaimPlaceholderIdentityProposalService>();
        services.AddScoped<CrewmatePermissionProposalService>();
        services.AddScoped<PlaceholderCrewmateService>();
        services.AddScoped<CrewGiftRecipientService>();
        services.AddScoped<EmergencySplitService>();
        services.AddScoped<ProposalAnonymousAliasService>();
        services.AddScoped<EmptyCrewCleanupService>();
        services.AddScoped<LibraryContributionGiftService>();
        services.AddScoped<LibraryRequestCleanupHelper>();
        services.AddScoped<LibraryMemberCleanupService>();
        services.AddScoped<NotificationService>();
        services.AddScoped<ContentMentionService>();

        return services;
    }
}
