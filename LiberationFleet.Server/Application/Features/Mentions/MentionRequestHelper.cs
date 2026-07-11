namespace LiberationFleet.Server.Application.Features.Mentions;

public static class MentionRequestHelper
{
    public static IReadOnlyList<int> Normalize(IEnumerable<int>? mentionedUserIds) =>
        mentionedUserIds?
            .Where(id => id > 0)
            .Distinct()
            .ToList()
        ?? [];
}
