using System.Security.Claims;
using LiberationFleet.Server.Application.Common.Interfaces;

namespace LiberationFleet.Server.Infrastructure.Services;

/// <summary>
/// Touches last-active for authenticated API and hub traffic.
/// </summary>
public sealed class UserPresenceMiddleware(RequestDelegate next)
{
    public async Task InvokeAsync(HttpContext context, IUserPresenceService presence)
    {
        if (context.User.Identity?.IsAuthenticated == true)
        {
            var path = context.Request.Path;
            if (IsPresencePath(path)
                && TryGetUserId(context.User, out var userId))
            {
                presence.RecordActivity(userId);
            }
        }

        await next(context);
    }

    private static bool IsPresencePath(PathString path) =>
        path.StartsWithSegments("/api")
        || path.StartsWithSegments("/hubs");

    private static bool TryGetUserId(ClaimsPrincipal user, out int userId)
    {
        var claim = user.FindFirstValue(ClaimTypes.NameIdentifier)
            ?? user.FindFirstValue("sub");
        return int.TryParse(claim, out userId);
    }
}
