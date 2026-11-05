using LiberationFleet.Server.Application.Common.Interfaces;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Common;

/// <summary>
/// Bidirectional block checks. Either person blocking the other hides content both ways.
/// Crewmate lists and role-gated stat downloads stay visible on purpose.
/// </summary>
public static class BlockVisibility
{
    public static bool Hides(IReadOnlySet<int> hiddenUserIds, int userId) =>
        hiddenUserIds.Contains(userId);

    public static bool HidesAny(IReadOnlySet<int> hiddenUserIds, int userId, int? otherUserId = null) =>
        hiddenUserIds.Contains(userId)
        || (otherUserId is int other && hiddenUserIds.Contains(other));

    public static bool HidesGift(IReadOnlySet<int> hiddenUserIds, Gift gift) =>
        hiddenUserIds.Contains(gift.GiverUserId)
        || hiddenUserIds.Contains(gift.RecipientUserId)
        || (gift.MiddlemanUserId is int middlemanUserId && hiddenUserIds.Contains(middlemanUserId));

    public static IReadOnlyList<T> OmitHiddenReceptionEntries<T>(
        IReadOnlyList<T> entries,
        IReadOnlySet<int> hiddenUserIds)
        where T : ReceptionOrderEntryDto
    {
        if (hiddenUserIds.Count == 0 || entries.Count == 0)
        {
            return entries;
        }

        var kept = new List<T>(entries.Count);
        foreach (var entry in entries)
        {
            if (hiddenUserIds.Contains(entry.UserId)
                || (entry.CycleUserId is int cycleUserId && hiddenUserIds.Contains(cycleUserId)))
            {
                continue;
            }

            if (entry.MiddlemanOptions.Count > 0)
            {
                var options = entry.MiddlemanOptions
                    .Where(option => !hiddenUserIds.Contains(option.UserId))
                    .ToList();
                if (options.Count != entry.MiddlemanOptions.Count)
                {
                    entry.MiddlemanOptions = options;
                    if (entry.DefaultMiddlemanId is int defaultMiddlemanId
                        && hiddenUserIds.Contains(defaultMiddlemanId))
                    {
                        entry.DefaultMiddlemanId = options.Count > 0 ? options[0].UserId : null;
                    }

                    if (options.Count == 0)
                    {
                        entry.NoSuitableMiddleman = true;
                    }
                }
            }

            kept.Add(entry);
        }

        return kept;
    }
}
