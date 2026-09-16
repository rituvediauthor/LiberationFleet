namespace LiberationFleet.Server.Infrastructure.Background;

/// <summary>
/// After authentication, schedules maintenance sweeps only for related API/hub paths
/// when those jobs are configured for <see cref="BackgroundJobRunMode.OnActivity"/>.
/// </summary>
public sealed class ActivityTriggeredBackgroundJobsMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, ActivityTriggeredBackgroundJobs jobs)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path;
            if (IsProposalRelated(path))
            {
                jobs.NotifyProposalActivity();
            }

            if (IsGiftRelated(path))
            {
                jobs.NotifyGiftActivity();
            }

            if (IsMediaRelated(path))
            {
                jobs.NotifyMediaActivity();
            }
        }

        await next(context);
    }

    private static bool IsProposalRelated(PathString path) =>
        path.StartsWithSegments("/api/proposals")
        || path.StartsWithSegments("/api/crews")
        || path.StartsWithSegments("/api/fleets")
        || path.StartsWithSegments("/api/crewmates")
        || path.StartsWithSegments("/api/rules")
        || path.StartsWithSegments("/api/notifications")
        || path.StartsWithSegments("/hubs/notifications");

    private static bool IsGiftRelated(PathString path) =>
        path.StartsWithSegments("/api/gifts")
        || path.StartsWithSegments("/api/season")
        || path.StartsWithSegments("/api/emergency-requests")
        || path.StartsWithSegments("/api/payment-platforms")
        || path.StartsWithSegments("/api/dev/mutual-aid");

    private static bool IsMediaRelated(PathString path) =>
        path.StartsWithSegments("/api/chats")
        || path.StartsWithSegments("/api/forums")
        || path.StartsWithSegments("/api/crypto")
        || path.StartsWithSegments("/hubs/chat")
        || path.StartsWithSegments("/hubs/voice");
}
