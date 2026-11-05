using LiberationFleet.Server.Application.Common;
using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Gifts;

internal static class GiftLogVisibility
{
    public static async Task<GiftLogPage> TakeVisiblePageAsync(
        Func<DateTime?, int?, CancellationToken, Task<GiftLogPage>> fetchPage,
        IReadOnlySet<int> hiddenUserIds,
        int limit,
        DateTime? beforeCreatedAt,
        int? beforeId,
        CancellationToken cancellationToken)
    {
        var visible = new List<Gift>();
        var cursorAt = beforeCreatedAt;
        var cursorId = beforeId;
        var hasMore = false;

        // Walk older pages when a block hides an entire window, so the client
        // still receives a page it can scroll from.
        for (var hop = 0; hop < 8 && visible.Count < limit; hop++)
        {
            var raw = await fetchPage(cursorAt, cursorId, cancellationToken);
            if (raw.Items.Count == 0)
            {
                hasMore = false;
                break;
            }

            var kept = raw.Items.Where(gift => !BlockVisibility.HidesGift(hiddenUserIds, gift)).ToList();
            visible.InsertRange(0, kept);
            hasMore = raw.HasMore;
            if (!raw.HasMore)
            {
                break;
            }

            var oldest = raw.Items[0];
            cursorAt = oldest.CreatedAt;
            cursorId = oldest.Id;
        }

        if (visible.Count > limit)
        {
            visible = visible.Skip(visible.Count - limit).ToList();
            hasMore = true;
        }

        return new GiftLogPage
        {
            Items = visible,
            HasMore = hasMore
        };
    }
}
