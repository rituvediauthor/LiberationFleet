using System.Linq.Expressions;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Notifications;

/// <summary>
/// Path match for marking notifications read. A prefix must end at a path boundary
/// so <c>/messages/1</c> does not also match <c>/messages/12</c>.
/// </summary>
public static class NotificationActionUrl
{
    public static Expression<Func<Notification, bool>> MatchesPrefix(string prefix)
    {
        var slash = prefix + "/";
        var query = prefix + "?";
        var hash = prefix + "#";
        return notification =>
            notification.ActionUrl == prefix
            || notification.ActionUrl.StartsWith(slash)
            || notification.ActionUrl.StartsWith(query)
            || notification.ActionUrl.StartsWith(hash);
    }
}
