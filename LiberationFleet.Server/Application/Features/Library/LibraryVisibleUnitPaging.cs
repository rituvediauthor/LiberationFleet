using LiberationFleet.Server.Application.Common.Interfaces.Persistence;
using LiberationFleet.Server.Domain.Entities;

namespace LiberationFleet.Server.Application.Features.Library;

/// <summary>
/// Pages library units after an in-memory visibility filter so client offsets
/// (visible-item counts) stay aligned with filtered results.
/// </summary>
public static class LibraryVisibleUnitPaging
{
    public static Task<(IReadOnlyList<LibraryUnit> Items, bool HasMore)> TakeVisibleAsync(
        Func<int, int, CancellationToken, Task<LibraryUnitListPage>> fetchPage,
        Func<LibraryUnit, bool> isVisible,
        int offset,
        int limit,
        CancellationToken cancellationToken) =>
        TakeVisibleAsync(
            fetchPage,
            (unit, _) => Task.FromResult(isVisible(unit)),
            offset,
            limit,
            cancellationToken);

    public static async Task<(IReadOnlyList<LibraryUnit> Items, bool HasMore)> TakeVisibleAsync(
        Func<int, int, CancellationToken, Task<LibraryUnitListPage>> fetchPage,
        Func<LibraryUnit, CancellationToken, Task<bool>> isVisibleAsync,
        int offset,
        int limit,
        CancellationToken cancellationToken)
    {
        var skipRemaining = Math.Max(offset, 0);
        var take = Math.Clamp(limit, 1, 100);
        var collected = new List<LibraryUnit>(take);
        var scanOffset = 0;
        var batchSize = Math.Max(take * 2, 40);

        while (collected.Count < take)
        {
            var page = await fetchPage(batchSize, scanOffset, cancellationToken);
            if (page.Items.Count == 0)
            {
                return (collected, false);
            }

            foreach (var unit in page.Items)
            {
                if (!await isVisibleAsync(unit, cancellationToken))
                {
                    continue;
                }

                if (skipRemaining > 0)
                {
                    skipRemaining--;
                    continue;
                }

                collected.Add(unit);
                if (collected.Count >= take)
                {
                    break;
                }
            }

            scanOffset += page.Items.Count;

            if (collected.Count >= take)
            {
                return (collected, await HasAnyVisibleBeyondAsync(
                    fetchPage,
                    isVisibleAsync,
                    page,
                    scanOffset,
                    collected[^1].Id,
                    cancellationToken));
            }

            if (!page.HasMore)
            {
                return (collected, false);
            }
        }

        return (collected, false);
    }

    private static async Task<bool> HasAnyVisibleBeyondAsync(
        Func<int, int, CancellationToken, Task<LibraryUnitListPage>> fetchPage,
        Func<LibraryUnit, CancellationToken, Task<bool>> isVisibleAsync,
        LibraryUnitListPage lastPage,
        int nextScanOffset,
        int lastTakenUnitId,
        CancellationToken cancellationToken)
    {
        var passedTaken = false;
        foreach (var unit in lastPage.Items)
        {
            if (!passedTaken)
            {
                if (unit.Id == lastTakenUnitId)
                {
                    passedTaken = true;
                }

                continue;
            }

            if (await isVisibleAsync(unit, cancellationToken))
            {
                return true;
            }
        }

        if (!lastPage.HasMore)
        {
            return false;
        }

        var scanOffset = nextScanOffset;
        const int peekBatch = 40;
        while (true)
        {
            var page = await fetchPage(peekBatch, scanOffset, cancellationToken);
            if (page.Items.Count == 0)
            {
                return false;
            }

            foreach (var unit in page.Items)
            {
                if (await isVisibleAsync(unit, cancellationToken))
                {
                    return true;
                }
            }

            scanOffset += page.Items.Count;
            if (!page.HasMore)
            {
                return false;
            }
        }
    }
}
